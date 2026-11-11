using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.ApiService.Features.BankConnection;
using KbcTracker.ApiService.Features.Shared;
using KbcTracker.ApiService.Features.Shared.Security;
using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.BankConnection.Services;
using KbcTracker.Application.Features.Transactions.Queries;
using KbcTracker.Infrastructure.Features.BankConnection;
using KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.ServiceDefaults.Features.Telemetry;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Pivot.Framework.Application.Messaging;

namespace KbcTracker.ApiService;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Hosts PostgreSQL-backed application endpoints independently of either UI.
/// </summary>
public sealed class Program
{
	#region Constants
	/// <summary>The local development listener when no port is configured.</summary>
	private const int DefaultPort = 5080;
	#endregion

	#region Constructors
	/// <summary>Prevents construction of the host entry point.</summary>
	private Program()
	{
	}
	#endregion

	#region Public Methods
	/// <summary>Starts the protected API, or explicitly applies migrations when requested.</summary>
	/// <param name="args">Host arguments, or one explicit administrative command.</param>
	/// <returns>The process exit code.</returns>
	/// <exception cref="InvalidOperationException">The connection string is missing.</exception>
	public static async Task<int> Main(string[] args)
	{
		var administrative = BackendAdministration.IsRequested(args);
		var builder = WebApplication.CreateBuilder(administrative ? [] : args);
		builder.AddServiceDefaults();
		builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, builder.Configuration.GetValue("BackendPort", DefaultPort)));
		ConfigureServices(builder);
		var app = builder.Build();
		if (administrative)
		{
			var code = await BackendAdministration.ExecuteAsync(args, app.Services, CancellationToken.None).ConfigureAwait(false);
			await app.DisposeAsync().ConfigureAwait(false);
			return code;
		}

		app.UseExceptionHandler();
		app.UseMiddleware<TailnetAccessMiddleware>();
		if (app.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		app.MapControllers();
		await app.RunAsync().ConfigureAwait(false);
		return 0;
	}
	#endregion

	#region Private Helpers
	/// <summary>Registers the scoped application and persistence services shared by all endpoints.</summary>
	private static void ConfigureServices(WebApplicationBuilder builder)
	{
		var connectionString = builder.Configuration.GetConnectionString("KbcTracker")
			?? throw new InvalidOperationException("Configure ConnectionStrings:KbcTracker before starting the backend.");
		builder.Services.AddKbcTrackerPersistence(connectionString);
		builder.Services.AddPivotMediator(options => options.RegisterServicesFromAssemblyContaining<ListTransactionsQuery>());
		builder.Services.AddScoped<SyncService>();
		builder.Services.AddHostedService<AutomaticBankSync>();
		builder.Services.AddSingleton(TimeProvider.System);
		var bank = builder.Configuration.GetSection("EnableBanking").Get<EnableBankingOptions>() ?? new();
		if (bank.ConsentDays is < 1 or > 180)
		{
			throw new InvalidOperationException("EnableBanking:ConsentDays must be between 1 and 180.");
		}

		builder.Services.AddSingleton(bank);
		builder.Services.AddSingleton(new SyncSettings(bank.HistoryMonths));
		builder.Services.AddHttpClient<EnableBankingClient>();
		builder.Services.AddTransient<IBankTransactionSource, EnableBankingTransactionSource>();
		builder.Services.AddSingleton<IConsentStateStore, ConsentStateStore>();
		builder.Services.AddScoped<BankConsentWorkflow>();
		builder.Services.AddTransient<IBankConsentSource, EnableBankingConsentSource>();
		builder.Services.AddScoped<IBankConsentPersistence, PostgreSqlBankConsentPersistence>();
		builder.Services.AddSingleton(new BankConsentSettings(bank.ConsentDays));
		builder.Services.Configure<BackendAccessOptions>(builder.Configuration.GetSection(BackendAccessOptions.SectionName));
		builder.Services.AddControllers()
			.AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
		builder.Services.AddProblemDetails();
		builder.Services.AddSwaggerGen(options =>
		{
			options.SwaggerDoc("v1", new() { Title = "KbcTracker backend", Version = "v1" });
			options.IncludeXmlComments(Path.ChangeExtension(typeof(BackendController).Assembly.Location, ".xml"));
			options.SupportNonNullableReferenceTypes();
			options.NonNullableReferenceTypesAsRequired();

			// Amounts are exact decimals; the default "double" format would invite clients to round them.
			options.MapType<decimal>(() => new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" });
			options.CustomOperationIds(api => api.ActionDescriptor is ControllerActionDescriptor action
				? JsonNamingPolicy.CamelCase.ConvertName(action.ActionName)
				: null);
		});
	}
	#endregion
}
