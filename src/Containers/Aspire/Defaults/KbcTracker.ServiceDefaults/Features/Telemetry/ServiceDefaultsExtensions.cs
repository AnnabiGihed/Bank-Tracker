using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace KbcTracker.ServiceDefaults.Features.Telemetry;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Gives every runnable host the same telemetry: logs, traces and metrics sent to the Aspire dashboard
///              when the AppHost starts the host, and nothing exported when it runs on its own.
/// </summary>
public static class ServiceDefaultsExtensions
{
	#region Constants
	/// <summary>The setting the AppHost sets to the dashboard's collector; without it nothing is exported.</summary>
	private const string OtlpEndpointSetting = "OTEL_EXPORTER_OTLP_ENDPOINT";
	#endregion

	#region Public Methods
	/// <summary>
	/// Adds logs, traces and metrics for ASP.NET Core, outgoing HTTP calls and the runtime, exported over OTLP only
	/// when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. The HTTP instrumentations keep their default redaction of
	/// query-string values, so the bank's authorization code and state never reach a trace.
	/// </summary>
	/// <typeparam name="TBuilder">The host builder type.</typeparam>
	/// <param name="builder">The host builder of the API or the web app.</param>
	/// <returns>The same builder, for chaining.</returns>
	public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
		where TBuilder : IHostApplicationBuilder
	{
		ArgumentNullException.ThrowIfNull(builder);
		builder.Logging.AddOpenTelemetry(logging =>
		{
			logging.IncludeFormattedMessage = true;
			logging.IncludeScopes = true;
		});
		var telemetry = builder.Services.AddOpenTelemetry()
			.WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation())
			.WithTracing(tracing => tracing.AddSource(builder.Environment.ApplicationName).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
		if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointSetting]))
		{
			telemetry.UseOtlpExporter();
		}

		return builder;
	}
	#endregion
}
