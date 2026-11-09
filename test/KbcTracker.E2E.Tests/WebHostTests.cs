using System.Net;
using System.Net.Http.Json;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Web.Features.Shared.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.E2E.Tests;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs the web host end to end in memory, over a temporary data folder with automatic syncs off:
///              every tab renders, and an action posted to <c>POST /api</c> is saved. Never touches the user's data
///              or the bank.
/// </summary>
public sealed class WebHostTests : IDisposable
{
	#region Fields
	/// <summary>
	/// The temporary data folder of this test.
	/// </summary>
	private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "kbc-e2e-" + Guid.NewGuid().ToString("N"));

	/// <summary>
	/// The web host under test.
	/// </summary>
	private readonly WebApplicationFactory<Program> _factory;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="WebHostTests"/>: starts the host and stores two months of transactions.
	/// </summary>
	public WebHostTests()
	{
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
			.UseSetting("DataDirectory", _dataDir)
			.UseSetting("AutoSyncHours", "0"));
		_factory.Services.GetRequiredService<Store>().Merge(
		[
			new Txn("t1", "acc-1", new DateOnly(2026, 10, 3), -12.50m, "EUR", "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
			new Txn("t2", "acc-1", new DateOnly(2026, 11, 3), 2500m, "EUR", "Acme Payroll", "Salary November"),
		]);
	}
	#endregion

	#region Page Tests
	/// <summary>
	/// Verifies that every tab renders for a month with data.
	/// </summary>
	/// <param name="path">The tab's address.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("/overview?m=2026-11")]
	[InlineData("/fixed?m=2026-11")]
	[InlineData("/transactions?m=2026-10")]
	[InlineData("/review?m=2026-11")]
	[InlineData("/categories?m=2026-11")]
	[InlineData("/settings?m=2026-11")]
	public async Task Get_Tab_RendersHtml(string path)
	{
		using var client = _factory.CreateClient();

		using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
		(await response.Content.ReadAsStringAsync()).Should().Contain("<html");
	}

	/// <summary>
	/// Verifies that the transactions tab lists a stored transaction.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Get_Transactions_ListsStoredTransaction()
	{
		using var client = _factory.CreateClient();

		var html = await client.GetStringAsync(new Uri("/transactions?m=2026-10", UriKind.Relative));

		html.Should().Contain("Shop One");
	}
	#endregion

	#region Api Tests
	/// <summary>
	/// Verifies that renaming a payee through <c>POST /api</c> is saved in the user's decisions.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_Rename_SavedInUserData()
	{
		using var client = _factory.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api", UriKind.Relative), new { action = "rename", key = "CARD:SHOP ONE", name = "My shop" });

		response.StatusCode.Should().Be(HttpStatusCode.OK);
		_factory.Services.GetRequiredService<Store>().LoadUserData().PayeeNames["CARD:SHOP ONE"].Should().Be("My shop");
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		_factory.Dispose();
		Directory.Delete(_dataDir, recursive: true);
	}
	#endregion
}
