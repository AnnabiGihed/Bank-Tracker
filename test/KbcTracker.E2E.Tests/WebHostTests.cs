using System.Net;
using System.Net.Http.Json;
using KbcTracker.Domain.Features.BankConnection.Aggregates;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
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
			Txn("t1", new DateOnly(2026, 10, 3), -12.50m, "Shop One", "SHOP ONE BE1000 Bruxelles Payment with card"),
			Txn("t2", new DateOnly(2026, 11, 3), 2500m, "Acme Payroll", "Salary November"),
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

	/// <summary>
	/// Verifies the sync notices on a page: nothing when no transaction was left out, the number left out with a
	/// link to sync the whole history again, and only the failure banner when the last sync failed.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task Get_SessionWithSkippedTransactions_NoticeShownUnlessSyncFailed()
	{
		using var client = _factory.CreateClient();
		var store = _factory.Services.GetRequiredService<Store>();
		var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

		store.SaveSession(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: null, skippedTransactions: 0));
		var withoutSkipped = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));
		store.SaveSession(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: null, skippedTransactions: 3));
		var afterSkipped = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));
		store.SaveSession(SessionInfo.Rehydrate(new BankSessionId("s"), ["acc-1"], start, start.AddDays(180), start, lastError: "Bank down", skippedTransactions: 3));
		var afterFailure = await client.GetStringAsync(new Uri("/overview", UriKind.Relative));

		withoutSkipped.Should().NotContain("could not be read");
		afterSkipped.Should().Contain("3 transaction(s) from the bank could not be read").And.Contain("href='/sync?full=true&back=/settings'");
		afterFailure.Should().Contain("Last sync failed").And.NotContain("could not be read");
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

	/// <summary>
	/// Verifies that requests missing a field the action needs, or with a blank name, are refused with 400 and a
	/// message instead of failing with 500.
	/// </summary>
	/// <param name="body">The request body.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("""{"action":"removeFixed"}""")]
	[InlineData("""{"action":"txnOnly","key":"CARD:SHOP ONE","id":"t1"}""")]
	[InlineData("""{"action":"rename","key":"CARD:SHOP ONE","name":" "}""")]
	[InlineData("""{"action":"saveCategory","name":"&&","type":"Expense","color":"#123456"}""")]
	[InlineData("""{"action":"unknown"}""")]
	public async Task PostApi_MissingOrInvalidField_BadRequest(string body)
	{
		using var client = _factory.CreateClient();
		using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

		using var response = await client.PostAsync(new Uri("/api", UriKind.Relative), content);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Verifies that actions on something that does not exist answer 404 with a message instead of failing.
	/// </summary>
	/// <param name="body">The request body.</param>
	/// <returns>A task that completes when the test has run.</returns>
	[Theory]
	[InlineData("""{"action":"accept","key":"NO:SUCH"}""")]
	[InlineData("""{"action":"mark","key":"NO:SUCH"}""")]
	[InlineData("""{"action":"removeFixed","id":"missing"}""")]
	[InlineData("""{"action":"deleteCategory","id":"missing"}""")]
	public async Task PostApi_UnknownTarget_NotFoundWithMessage(string body)
	{
		using var client = _factory.CreateClient();
		using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

		using var response = await client.PostAsync(new Uri("/api", UriKind.Relative), content);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>
	/// Verifies that dismissing a suggestion stores its signature, and that confirming payees through the single
	/// key or the list of keys stores their automatic category as confirmed.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_DismissAndConfirm_Saved()
	{
		using var client = _factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);
		var store = _factory.Services.GetRequiredService<Store>();
		var analysis = new Analysis(store.LoadTransactions(), store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal)), store.LoadUserData());
		var keys = analysis.Items.Select(i => i.SeriesKey).ToList();

		using var dismiss = await client.PostAsJsonAsync(api, new { action = "dismiss", key = "SIGNATURE:X" });
		using var confirmOne = await client.PostAsJsonAsync(api, new { action = "confirm", key = keys[0] });
		using var confirmAll = await client.PostAsJsonAsync(api, new { action = "confirm", keys });

		dismiss.StatusCode.Should().Be(HttpStatusCode.OK);
		confirmOne.StatusCode.Should().Be(HttpStatusCode.OK);
		confirmAll.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = store.LoadUserData();
		user.DismissedSuggestions.Should().Contain("SIGNATURE:X");
		user.Payees.Keys.Should().BeEquivalentTo(keys);
		user.Payees.Values.Should().OnlyContain(p => p.Confirmed);
	}

	/// <summary>
	/// Verifies the payee-category flow of the Transactions tab: setting a payee category returns the previous
	/// assignment, and "only this one" sends it back to restore the payee and set the transaction's own category.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_PayeeCategoryThenOnlyThisOne_PayeeRestoredTransactionSet()
	{
		using var client = _factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);
		using var first = await client.PostAsJsonAsync(api, new { action = "payeeCategory", key = "CARD:SHOP ONE", categoryId = "bills" });
		using var second = await client.PostAsJsonAsync(api, new { action = "payeeCategory", key = "CARD:SHOP ONE", categoryId = "cash" });
		var previous = await second.Content.ReadFromJsonAsync<PreviousAssignment>();

		using var onlyThis = await client.PostAsJsonAsync(api, new { action = "txnOnly", key = "CARD:SHOP ONE", id = "t1", categoryId = "cash", restore = previous });

		previous.Should().Be(new PreviousAssignment("bills", Confirmed: true));
		onlyThis.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = _factory.Services.GetRequiredService<Store>().LoadUserData();
		user.Payees["CARD:SHOP ONE"].CategoryId!.Value.Should().Be("bills");
		user.TxnCategories.Single().Value.Value.Should().Be("cash");
	}

	/// <summary>
	/// Verifies the fixed-payment flow: marking a payee as fixed adds it, and stopping it removes it and dismisses
	/// its suggestion so it does not come back.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_MarkThenRemoveFixed_RemovedAndDismissed()
	{
		using var client = _factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);
		var store = _factory.Services.GetRequiredService<Store>();

		var analysis = new Analysis(store.LoadTransactions(), store.LoadCategories(new Dictionary<string, string[]>(StringComparer.Ordinal)), store.LoadUserData());
		var seriesKey = analysis.Items.Single(i => i.T.Id.Equals(new TxnId("t1"))).SeriesKey;

		using var mark = await client.PostAsJsonAsync(api, new { action = "mark", key = seriesKey });
		var fixedPayment = store.LoadUserData().Recurring.Single();
		using var remove = await client.PostAsJsonAsync(api, new { action = "removeFixed", id = fixedPayment.Id.Value });

		mark.StatusCode.Should().Be(HttpStatusCode.OK);
		remove.StatusCode.Should().Be(HttpStatusCode.OK);
		var user = store.LoadUserData();
		user.Recurring.Should().BeEmpty();
		user.DismissedSuggestions.Should().Contain(fixedPayment.Signature);
	}

	/// <summary>
	/// Verifies that changing a fixed payment that does not exist answers 404 instead of failing.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_UpdateUnknownFixedPayment_NotFound()
	{
		using var client = _factory.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api", UriKind.Relative), new { action = "updateFixed", id = "missing", name = "Rent" });

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>
	/// Verifies that the built-in <c>other</c> category cannot be deleted, and that a new category can be created
	/// and deleted.
	/// </summary>
	/// <returns>A task that completes when the test has run.</returns>
	[Fact]
	public async Task PostApi_Categories_OtherProtectedAndNewOneDeletable()
	{
		using var client = _factory.CreateClient();
		var api = new Uri("/api", UriKind.Relative);

		using var deleteOther = await client.PostAsJsonAsync(api, new { action = "deleteCategory", id = "other" });
		using var save = await client.PostAsJsonAsync(api, new { action = "saveCategory", name = "Pets", type = "Expense", color = "#123456", keywords = "VET, ZOO" });
		var created = await save.Content.ReadFromJsonAsync<CreatedCategory>();
		using var deleteNew = await client.PostAsJsonAsync(api, new { action = "deleteCategory", id = created!.Id });

		deleteOther.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		save.StatusCode.Should().Be(HttpStatusCode.OK);
		created.Id.Should().Be("pets");
		deleteNew.StatusCode.Should().Be(HttpStatusCode.OK);
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

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The answer of <c>saveCategory</c>.
	/// </summary>
	/// <param name="Id">The category id.</param>
	private sealed record CreatedCategory(string Id);

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : The answer of <c>payeeCategory</c>: the payee's assignment before the change.
	/// </summary>
	/// <param name="CategoryId">The previous category id.</param>
	/// <param name="Confirmed">Whether it was confirmed.</param>
	private sealed record PreviousAssignment(string? CategoryId, bool Confirmed);
	#endregion

	#region Private Helpers
	/// <summary>
	/// A transaction of the test account without bank JSON, as older histories hold.
	/// </summary>
	private static Txn Txn(string id, DateOnly date, decimal amount, string counterparty, string description) =>
		Domain.Features.Transactions.Aggregates.Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate(counterparty, null), description, BankRecord.None);
	#endregion
}
