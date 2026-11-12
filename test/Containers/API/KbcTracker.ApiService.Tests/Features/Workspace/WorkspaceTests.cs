using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.Workspace;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the central workspace preserves classification and every existing decision across HTTP and PostgreSQL.
/// </summary>
/// <param name="fixture">The isolated real-database API host.</param>
public sealed class WorkspaceTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Fields
	/// <summary>The API's JSON format, with enums as their names.</summary>
	private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Snapshot Tests
	/// <summary>Verifies raw-source classification survives JSON round trips without exposing source records or credentials.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Workspace_RawSourceClassification_PreservedWithoutRawTransport()
	{
		using var factory = fixture.CreateFactory("Development");
		var scope = factory.Services.CreateAsyncScope();
		await using (scope)
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			db.Transactions.Add(Txn.Rehydrate(new TxnId("workspace-loan"), "private-account", new DateOnly(2026, 10, 2),
				Money.Rehydrate(-75m, "EUR"), Counterparty.Rehydrate("Bank", "BE99999999999999"), "Reference TEST-LOAN",
				new BankRecord("PMNT", """{"bank_transaction_code":{"code":"CSLN"},"debtor":{"name":"Holder Example"},"private_marker":"do-not-transport-this"}""")));
			await db.SaveChangesAsync();
		}

		using var client = fixture.CreateClient();
		var text = await client.GetStringAsync(new Uri("/api/v1/workspace", UriKind.Relative));
		text.Should().NotContain(ApiFixture.RawMarker).And.NotContain(ApiFixture.PrivateSession)
			.And.NotContain("private-account").And.NotContain("do-not-transport-this").And.NotContain("BE99999999999999");
		var snapshot = JsonSerializer.Deserialize<WorkspaceSnapshot>(text, ApiJson)
			?? throw new InvalidOperationException("The workspace response must be present.");
		var restored = snapshot.ToAnalysis();
		var item = restored.Items.Single(i => string.Equals(i.T.Id.Value, "workspace-loan", StringComparison.Ordinal));
		item.Kind.Should().Be(Kind.Loan);
		item.SeriesKey.Should().Be("LOAN:TEST-LOAN");
		restored.HolderName.Should().Be(Analysis.Norm("Holder Example"));
		item.T.Raw.Should().BeNull();
		item.T.CounterpartyIban.Should().BeNull();
		restored.Months.Should().BeEquivalentTo(snapshot.Analysis.Months);
		AnalysisSnapshot.From(restored, snapshot.Analysis.Today).Should().BeEquivalentTo(snapshot.Analysis);
		snapshot.ToDisplaySession().Should().NotBeNull();
		snapshot.ToDisplaySession()!.AccountUids.Count.Should().Be(1);
	}
	#endregion

	#region Decision Tests
	/// <summary>Verifies category edits and transaction-only undo survive separate HTTP scopes and category removal clears references.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_CategoryAndPayeeEdits_CommitCentrally()
	{
		using var client = fixture.CreateClient();
		var before = await WorkspaceAsync(client);
		var key = before.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).SeriesKey;
		var created = await ApplyAsync(client, new("saveCategory", Name: "Workspace test", Type: "Expense", Color: "#123456", Keywords: "Shop"));
		created.Id.Should().Be("workspace-test");
		var assigned = await ApplyAsync(client, new("payeeCategory", Key: key, CategoryId: created.Id));
		await ApplyAsync(client, new("txnOnly", Key: key, Id: "historical", CategoryId: created.Id, Restore: assigned.PreviousAssignment));
		await ApplyAsync(client, new("rename", Key: key, Name: "Chosen workspace name"));
		var edited = await WorkspaceAsync(client);
		edited.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryId.Should().Be(created.Id);
		edited.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).Name.Should().Be("Chosen workspace name");
		await ApplyAsync(client, new("saveCategory", Id: created.Id, Name: "Updated workspace", Color: "#654321"));
		(await WorkspaceAsync(client)).Analysis.Categories.Single(c => string.Equals(c.Id, created.Id, StringComparison.Ordinal)).Name.Should().Be("Updated workspace");
		await ApplyAsync(client, new("deleteCategory", Id: created.Id));
		var removed = await WorkspaceAsync(client);
		removed.Analysis.Categories.Should().NotContain(c => string.Equals(c.Id, created.Id, StringComparison.Ordinal));
		removed.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryId.Should().NotBe(created.Id);
	}

	/// <summary>Verifies marking, editing, removing and dismissing fixed payments remains available through the backend.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_FixedPaymentLifecycle_Persisted()
	{
		using var client = fixture.CreateClient();
		var before = await WorkspaceAsync(client);
		var key = before.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).SeriesKey;
		await ApplyAsync(client, new("mark", Key: key));
		var marked = await WorkspaceAsync(client);
		var payment = marked.Analysis.Fixed.Single(f => f.Definition.SeriesKeys.Contains(key, StringComparer.Ordinal));
		await ApplyAsync(client, new("updateFixed", Id: payment.Definition.Id, Name: "Tracked payment", Amount: 42m, Cadence: 91));
		var updated = (await WorkspaceAsync(client)).Analysis.Fixed.Single(f => string.Equals(f.Definition.Id, payment.Definition.Id, StringComparison.Ordinal));
		updated.Definition.Name.Should().Be("Tracked payment");
		updated.Definition.Amount.Should().Be(42m);
		updated.Definition.CadenceDays.Should().Be(91);
		await ApplyAsync(client, new("removeFixed", Id: payment.Definition.Id));
		(await WorkspaceAsync(client)).Analysis.Fixed.Should().NotContain(f => string.Equals(f.Definition.Id, payment.Definition.Id, StringComparison.Ordinal));
		await ApplyAsync(client, new("dismiss", Key: "explicitly-dismissed-series"));
		await ApplyAsync(client, new("confirm", Keys: [key]));
		(await WorkspaceAsync(client)).Analysis.Items.Where(i => string.Equals(i.SeriesKey, key, StringComparison.Ordinal))
			.Should().OnlyContain(i => i.CategoryConfirmed);
	}

	/// <summary>Verifies accepting a computed suggestion turns it into a fixed payment and removes the suggestion.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_AcceptSuggestion_BecomesFixedPayment()
	{
		var today = DateOnly.FromDateTime(DateTime.UtcNow);
		using var factory = fixture.CreateFactory("Development");
		var scope = factory.Services.CreateAsyncScope();
		await using (scope)
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			db.Transactions.AddRange(LoanInstalment("accept-loan-1", today.AddDays(-35)), LoanInstalment("accept-loan-2", today.AddDays(-5)));
			await db.SaveChangesAsync();
		}

		using var client = fixture.CreateClient();
		var suggestion = (await WorkspaceAsync(client)).Analysis.Suggestions
			.Single(s => s.SeriesKeys.Contains("LOAN:ACCEPT-LOAN", StringComparer.Ordinal));
		await ApplyAsync(client, new("accept", Key: suggestion.Signature));
		var accepted = await WorkspaceAsync(client);
		accepted.Analysis.Fixed.Should().Contain(f => f.Definition.SeriesKeys.Contains("LOAN:ACCEPT-LOAN", StringComparer.Ordinal));
		accepted.Analysis.Suggestions.Should().NotContain(s => string.Equals(s.Signature, suggestion.Signature, StringComparison.Ordinal));
	}

	/// <summary>Verifies malformed decisions are refused without becoming successful no-op writes.</summary>
	/// <param name="action">The requested action.</param>
	/// <param name="expected">The expected validation or missing-selection status.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("unknown", HttpStatusCode.BadRequest)]
	[InlineData("rename", HttpStatusCode.BadRequest)]
	[InlineData("deleteCategory", HttpStatusCode.BadRequest)]
	public async Task Decisions_MissingOrUnsupportedFields_Rejected(string action, HttpStatusCode expected)
	{
		using var client = fixture.CreateClient();
		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest(action));
		response.StatusCode.Should().Be(expected);
		response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
	}

	/// <summary>Verifies stale selections and protected categories retain their HTTP failure semantics.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_StaleSuggestionAndProtectedCategory_Refused()
	{
		using var client = fixture.CreateClient();
		using var missing = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest("accept", Key: "missing"));
		missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
		using var protectedCategory = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), new DecisionRequest("deleteCategory", Id: "other"));
		protectedCategory.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}
	#endregion

	#region Category Tree Tests
	/// <summary>Verifies a child can be created under a parent, moved under another parent and made top level again,
	/// and that the child takes its root's type.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_ChildCreatedAndMoved_TreeUpdated()
	{
		using var client = fixture.CreateClient();
		var first = await ApplyAsync(client, new("saveCategory", Name: "Move root one", Type: "Savings"));
		var second = await ApplyAsync(client, new("saveCategory", Name: "Move root two", Type: "Savings"));
		var child = await ApplyAsync(client, new("saveCategory", Name: "Move child", ParentId: first.Id));

		(await CategoryAsync(client, child.Id!)).ParentId.Should().Be(first.Id);
		(await CategoryAsync(client, child.Id!)).Type.Should().Be(KbcTracker.Domain.Features.Categories.ValueObjects.CategoryType.Savings);
		await ApplyAsync(client, new("saveCategory", Id: child.Id, Name: "Move child", ParentId: second.Id));
		(await CategoryAsync(client, child.Id!)).ParentId.Should().Be(second.Id);
		await ApplyAsync(client, new("saveCategory", Id: child.Id, Name: "Move child", ParentId: ""));
		(await CategoryAsync(client, child.Id!)).ParentId.Should().BeNull();
	}

	/// <summary>Verifies a cycle, a tree deeper than four levels and a type that differs from the root are refused with a 400.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_CycleTooDeepTypeMismatch_Rejected()
	{
		using var client = fixture.CreateClient();
		var root = await ApplyAsync(client, new("saveCategory", Name: "Rule root"));
		var one = await ApplyAsync(client, new("saveCategory", Name: "Rule one", ParentId: root.Id));
		var two = await ApplyAsync(client, new("saveCategory", Name: "Rule two", ParentId: one.Id));
		var three = await ApplyAsync(client, new("saveCategory", Name: "Rule three", ParentId: two.Id));

		(await StatusAsync(client, new("saveCategory", Id: root.Id, Name: "Rule root", ParentId: three.Id))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("saveCategory", Name: "Rule four", ParentId: three.Id))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("saveCategory", Name: "Rule income", Type: "Income", ParentId: root.Id))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("saveCategory", Name: "Rule orphan", ParentId: "no-such-parent"))).Should().Be(HttpStatusCode.BadRequest);
	}

	/// <summary>Verifies deleting a node sends its payments to its parent and moves its children up, and deleting a
	/// top-level category sends them to the built-in other category.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_DeleteNode_PaymentsToParentChildrenUp()
	{
		using var client = fixture.CreateClient();
		var key = (await WorkspaceAsync(client)).Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).SeriesKey;
		var root = await ApplyAsync(client, new("saveCategory", Name: "Delete root"));
		var middle = await ApplyAsync(client, new("saveCategory", Name: "Delete middle", ParentId: root.Id));
		var leaf = await ApplyAsync(client, new("saveCategory", Name: "Delete leaf", ParentId: middle.Id));
		await ApplyAsync(client, new("payeeCategory", Key: key, CategoryId: middle.Id));

		await ApplyAsync(client, new("deleteCategory", Id: middle.Id));

		var afterNode = await WorkspaceAsync(client);
		afterNode.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryId.Should().Be(root.Id);
		afterNode.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryConfirmed.Should().BeTrue();
		afterNode.Analysis.Categories.Single(c => string.Equals(c.Id, leaf.Id, StringComparison.Ordinal)).ParentId.Should().Be(root.Id);
		await ApplyAsync(client, new("deleteCategory", Id: root.Id));
		var afterRoot = await WorkspaceAsync(client);
		afterRoot.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryId.Should().Be("other");
		afterRoot.Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).CategoryConfirmed.Should().BeFalse();
		afterRoot.Analysis.Categories.Single(c => string.Equals(c.Id, leaf.Id, StringComparison.Ordinal)).ParentId.Should().BeNull();
	}

	/// <summary>Verifies a built-in category can be renamed and recoloured but neither moved under another category
	/// nor given another type.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_BuiltInMovedOrRetyped_RejectedRenameAllowed()
	{
		using var client = fixture.CreateClient();
		var root = await ApplyAsync(client, new("saveCategory", Name: "Builtin root"));

		(await StatusAsync(client, new("saveCategory", Id: "other", Name: "Other", ParentId: root.Id))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("saveCategory", Id: "other", Name: "Other", Type: "Income"))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("saveCategory", Id: "other", Name: "Everything else", Type: "Expense", Color: "#654321"))).Should().Be(HttpStatusCode.OK);
		var renamed = await CategoryAsync(client, "other");
		renamed.Name.Should().Be("Everything else");
		renamed.ParentId.Should().BeNull();
	}

	/// <summary>Verifies decisions that name an unknown category are refused instead of pointing to nothing.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_UnknownCategory_Rejected()
	{
		using var client = fixture.CreateClient();
		var key = (await WorkspaceAsync(client)).Analysis.Items.Single(i => string.Equals(i.Id, "historical", StringComparison.Ordinal)).SeriesKey;

		(await StatusAsync(client, new("payeeCategory", Key: key, CategoryId: "no-such-category"))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("txnOnly", Key: key, Id: "historical", CategoryId: "no-such-category"))).Should().Be(HttpStatusCode.BadRequest);
		(await StatusAsync(client, new("updateFixed", Id: "any-fixed-payment", CategoryId: "no-such-category"))).Should().Be(HttpStatusCode.BadRequest);
	}
	#endregion

	#region Private Helpers
	/// <summary>Creates a monthly loan instalment as KBC sends it, so the backend suggests it as a fixed payment.</summary>
	private static Txn LoanInstalment(string id, DateOnly date) => Txn.Rehydrate(new TxnId(id), "current", date,
		Money.Rehydrate(-120m, "EUR"), Counterparty.Rehydrate("KBC BANK NV", null), "Loan repayment Reference ACCEPT-LOAN capital and interest",
		new BankRecord("PMNT", """{"bank_transaction_code":{"code":"CSLN","sub_code":"PPAY"},"credit_debit_indicator":"DBIT"}"""));

	/// <summary>Reads the current workspace over HTTP.</summary>
	private static async Task<WorkspaceSnapshot> WorkspaceAsync(HttpClient client) =>
		await client.GetFromJsonAsync<WorkspaceSnapshot>(new Uri("/api/v1/workspace", UriKind.Relative), ApiJson).ConfigureAwait(false)
		?? throw new InvalidOperationException("The workspace must be returned.");

	/// <summary>Posts a decision and returns only the HTTP status.</summary>
	private static async Task<HttpStatusCode> StatusAsync(HttpClient client, DecisionRequest request)
	{
		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request).ConfigureAwait(false);
		return response.StatusCode;
	}

	/// <summary>Reads one category from the current workspace.</summary>
	private static async Task<CategorySnapshot> CategoryAsync(HttpClient client, string id) =>
		(await WorkspaceAsync(client).ConfigureAwait(false)).Analysis.Categories.Single(c => string.Equals(c.Id, id, StringComparison.Ordinal));

	/// <summary>Commits a decision and reads its success payload.</summary>
	private static async Task<DecisionResponse> ApplyAsync(HttpClient client, DecisionRequest request)
	{
		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<DecisionResponse>().ConfigureAwait(false)
			?? throw new InvalidOperationException("The decision result must be returned.");
	}
	#endregion
}
