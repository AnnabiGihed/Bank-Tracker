using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Debts.ValueObjects;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.Workspace;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the debt decisions of ADR-0010 across HTTP and PostgreSQL: a debt added with a plan appears in the
///              workspace with its schedule and repayments, every action refuses what it must (missing fields, a bad plan,
///              a received payment that is missing, no loan credit or taken, a payment that is no debit or counts for
///              another debt), and unknown targets answer 404.
/// </summary>
/// <param name="fixture">The isolated real-database API host.</param>
public sealed class DebtDecisionTests(ApiFixture fixture) : IClassFixture<ApiFixture>, IAsyncLifetime
{
	#region Constants
	/// <summary>The IBAN of the invented lender.</summary>
	private const string LenderIban = "BE00111122223333";
	#endregion

	#region Static Instances
	/// <summary>The API's JSON format, with enums as their names.</summary>
	private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

	/// <summary>The day the invented loans were borrowed: the day of the seeded payouts.</summary>
	private static readonly DateOnly Borrowed = new(2026, 10, 1);

	/// <summary>Five monthly instalments of 1000, the plan most tests use.</summary>
	private static readonly RepaymentPlanRequest MonthlyPlan = new(5000m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, 1, 1000m);
	#endregion

	#region Fields
	/// <summary>The API host of this test, used to seed payments.</summary>
	private WebApplicationFactory<Program>? _factory;
	#endregion

	#region Theory Data
	/// <summary>Gets debts the backend must refuse as bad requests.</summary>
	public static TheoryData<DecisionRequest> InvalidDebts => new()
	{
		new DecisionRequest("addDebt"),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, " ", LenderIban, Plan: MonthlyPlan, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, new string('x', 101), LenderIban, Plan: MonthlyPlan, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", LenderIban, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", LenderIban, Principal: 10.001m, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", "BE-00", Plan: MonthlyPlan, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.BankLoan, "Bank", SeriesKey: "CARD:SHOP", Plan: MonthlyPlan, BorrowedOn: Borrowed)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", LenderIban, Plan: MonthlyPlan)),
		new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", LenderIban, Plan: MonthlyPlan, BorrowedOn: new DateOnly(1999, 12, 31))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(0m, new DateOnly(2026, 11, 1)))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100.005m, new DateOnly(2026, 11, 1)))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, InstalmentAmount: 10m, Count: 10))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, Count: 0))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, Count: 601))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, InstalmentAmount: 100.01m))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, 0, 10m))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Monthly, 32, 10m))),
		new DecisionRequest("addDebt", Debt: Planned(new RepaymentPlanRequest(100m, new DateOnly(2026, 11, 1), RepaymentFrequency.Weekly, 5, 10m))),
		new DecisionRequest("updateDebt", Id: "x"),
		new DecisionRequest("removeDebt"),
		new DecisionRequest("matchRepayment", Id: "x"),
		new DecisionRequest("unmatchRepayment", Key: "x"),
		new DecisionRequest("resetRepayment"),
	};
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		_factory = fixture.CreateFactory("Development");
		var scope = _factory.Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			if (await db.Transactions.AnyAsync(t => t.Id == new TxnId("debt-wages")).ConfigureAwait(false))
			{
				return;
			}

			AddCredit(db, "debt-payout-1", 5000m, new BankRecord("LDAS", null));
			AddCredit(db, "debt-payout-3", 800m, new BankRecord("LDAS", null));
			AddCredit(db, "debt-payout-4", 600m, new BankRecord("LDAS", null));
			AddCredit(db, "debt-wages", 2500m, BankRecord.None);
			AddDebit(db, "debt-out-1", -250m);
			AddDebit(db, "debt-out-2", -300m);
			AddDebit(db, "debt-out-3", -100m);
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_factory is not null)
		{
			await _factory.DisposeAsync().ConfigureAwait(false);
		}
	}
	#endregion

	#region Validation Tests
	/// <summary>Verifies a request with a missing field, a bad plan or an invalid value is refused with 400 and a message.</summary>
	/// <param name="request">The decision to send.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[MemberData(nameof(InvalidDebts))]
	public async Task Debts_InvalidRequest_BadRequestWithMessage(DecisionRequest request)
	{
		using var client = fixture.CreateClient();

		var (status, text) = await PostAsync(client, request);

		status.Should().Be(HttpStatusCode.BadRequest);
		text.Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>Verifies a received payment that does not exist answers 404, and one that is no loan credit answers 400.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_ReceivedPayment_MustExistAndBeALoanCredit()
	{
		using var client = fixture.CreateClient();

		var missing = await PostAsync(client, AddDebt(received: "debt-missing"));
		var wages = await PostAsync(client, AddDebt(received: "debt-wages"));
		var debit = await PostAsync(client, AddDebt(received: "debt-out-1"));

		missing.Status.Should().Be(HttpStatusCode.NotFound);
		wages.Status.Should().Be(HttpStatusCode.BadRequest);
		debit.Status.Should().Be(HttpStatusCode.BadRequest);
		(await ReadDebtsAsync(client)).Should().NotContain(d => Is(d.ReceivedPaymentId, "debt-wages") || Is(d.ReceivedPaymentId, "debt-out-1"));
	}

	/// <summary>Verifies a received payment cannot belong to two debts.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_ReceivedPaymentOfAnotherDebt_Refused()
	{
		using var client = fixture.CreateClient();
		var first = await PostAsync(client, AddDebt("Taken Lender", "debt-payout-3", MonthlyPlan));

		var second = await PostAsync(client, AddDebt("Second Lender", "debt-payout-3", MonthlyPlan));

		first.Status.Should().Be(HttpStatusCode.OK);
		second.Status.Should().Be(HttpStatusCode.BadRequest);
		(await ReadDebtsAsync(client)).Count(d => Is(d.ReceivedPaymentId, "debt-payout-3")).Should().Be(1);
	}

	/// <summary>Verifies every action on a debt that does not exist answers 404.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_UnknownDebt_NotFound()
	{
		using var client = fixture.CreateClient();

		var answers = new[]
		{
			await PostAsync(client, new DecisionRequest("updateDebt", Id: "missing", Debt: new DebtRequest(DebtKind.PersonalLoan, "Friend", Plan: MonthlyPlan, BorrowedOn: Borrowed))),
			await PostAsync(client, new DecisionRequest("removeDebt", Id: "missing")),
			await PostAsync(client, new DecisionRequest("matchRepayment", Id: "missing", Key: "debt-out-1")),
			await PostAsync(client, new DecisionRequest("unmatchRepayment", Id: "missing", Key: "debt-out-1")),
			await PostAsync(client, new DecisionRequest("resetRepayment", Id: "missing", Key: "debt-out-1")),
		};

		answers.Select(a => a.Status).Should().AllBeEquivalentTo(HttpStatusCode.NotFound);
	}
	#endregion

	#region Workspace Tests
	/// <summary>Verifies an added debt appears in the workspace with its plan, its schedule and the loan credit it tracks, and the
	/// credit leaves the unplanned list.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_AddDebt_AppearsInTheWorkspaceWithItsSchedule()
	{
		using var client = fixture.CreateClient();
		var before = await ReadSnapshotAsync(client);

		var added = await PostAsync(client, AddDebt("Workspace Lender", "debt-payout-1", MonthlyPlan));

		before.Analysis.UnplannedLoanIds.Should().Contain("debt-payout-1");
		added.Status.Should().Be(HttpStatusCode.OK);
		var response = JsonSerializer.Deserialize<DecisionResponse>(added.Text, ApiJson);
		var snapshot = await ReadSnapshotAsync(client);
		var debt = snapshot.Analysis.Debts.Single(d => Is(d.Lender, "Workspace Lender"));
		debt.Id.Should().Be(response!.Id);
		debt.Principal.Should().Be(5000m, "The amount received is the credit's amount.");
		debt.Plan!.Count.Should().Be(5);
		debt.Plan.LastInstalmentAmount.Should().Be(1000m);
		debt.Schedule.Should().HaveCount(5);
		debt.State.Should().NotBe(DebtState.NoPlan);
		snapshot.Analysis.UnplannedLoanIds.Should().NotContain("debt-payout-1");
		snapshot.ToAnalysis().Debts.Should().Contain(d => Is(d.Debt.Lender, "Workspace Lender"));
	}

	/// <summary>Verifies the day borrowed defaults to the date of the received payment, and the owner's own day wins.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_BorrowedOn_DefaultsToThePayoutDateAndIsKeptWhenGiven()
	{
		using var client = fixture.CreateClient();
		var own = new DateOnly(2026, 9, 20);

		var byPayout = await PostAsync(client, AddDebt("Default Day Lender", "debt-payout-4", MonthlyPlan));
		var given = await PostAsync(client, new DecisionRequest("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, "Given Day Lender", LenderIban, Principal: 100m, BorrowedOn: own)));

		byPayout.Status.Should().Be(HttpStatusCode.OK);
		given.Status.Should().Be(HttpStatusCode.OK);
		var debts = await ReadDebtsAsync(client);
		debts.Single(d => Is(d.Lender, "Default Day Lender")).BorrowedOn.Should().Be(Borrowed);
		debts.Single(d => Is(d.Lender, "Given Day Lender")).BorrowedOn.Should().Be(own);
	}

	/// <summary>Verifies a debt without a plan is stored as "no plan yet" and keeps the amount given.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_AddDebtWithoutPlan_NoPlanState()
	{
		using var client = fixture.CreateClient();

		var added = await PostAsync(client, AddDebt("No Plan Lender", plan: null, principal: 450m));

		added.Status.Should().Be(HttpStatusCode.OK);
		var debt = (await ReadDebtsAsync(client)).Single(d => Is(d.Lender, "No Plan Lender"));
		debt.State.Should().Be(DebtState.NoPlan);
		debt.Principal.Should().Be(450m);
		debt.Plan.Should().BeNull();
	}

	/// <summary>Verifies updating a debt changes what the owner said and keeps its id, and removing it takes it out of the workspace.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_UpdateThenRemove_ChangedThenGone()
	{
		using var client = fixture.CreateClient();
		var added = await PostAsync(client, AddDebt("Before Update", plan: MonthlyPlan));
		var id = JsonSerializer.Deserialize<DecisionResponse>(added.Text, ApiJson)!.Id!;

		var update = await PostAsync(client, new DecisionRequest("updateDebt", Id: id, Debt: new DebtRequest(DebtKind.PersonalLoan, "After Update", LenderIban, Plan: new RepaymentPlanRequest(300m, new DateOnly(2026, 12, 1)), BorrowedOn: Borrowed)));
		var changed = (await ReadDebtsAsync(client)).Single(d => Is(d.Id, id));
		var remove = await PostAsync(client, new DecisionRequest("removeDebt", Id: id));
		var removeAgain = await PostAsync(client, new DecisionRequest("removeDebt", Id: id));

		update.Status.Should().Be(HttpStatusCode.OK);
		changed.Lender.Should().Be("After Update");
		changed.Plan!.Count.Should().Be(1);
		remove.Status.Should().Be(HttpStatusCode.OK);
		removeAgain.Status.Should().Be(HttpStatusCode.NotFound);
		(await ReadDebtsAsync(client)).Should().NotContain(d => Is(d.Id, id));
	}

	/// <summary>Verifies matching, unmatching and resetting a payment move it between the debt's lists, and a payment counts for one
	/// debt only.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_MatchUnmatchReset_MovesThePaymentBetweenLists()
	{
		using var client = fixture.CreateClient();
		var first = JsonSerializer.Deserialize<DecisionResponse>((await PostAsync(client, AddDebt("Match Lender A", iban: "BE99000011110001", plan: MonthlyPlan))).Text, ApiJson)!.Id!;
		var second = JsonSerializer.Deserialize<DecisionResponse>((await PostAsync(client, AddDebt("Match Lender B", iban: "BE99000011110002", plan: MonthlyPlan))).Text, ApiJson)!.Id!;

		var match = await PostAsync(client, new DecisionRequest("matchRepayment", Id: first, Key: "debt-out-2"));
		var takenElsewhere = await PostAsync(client, new DecisionRequest("matchRepayment", Id: second, Key: "debt-out-2"));
		var matched = (await ReadDebtsAsync(client)).Single(d => Is(d.Id, first));
		var unmatch = await PostAsync(client, new DecisionRequest("unmatchRepayment", Id: first, Key: "debt-out-2"));
		var unmatched = (await ReadDebtsAsync(client)).Single(d => Is(d.Id, first));
		var reset = await PostAsync(client, new DecisionRequest("resetRepayment", Id: first, Key: "debt-out-2"));
		var resetDebt = (await ReadDebtsAsync(client)).Single(d => Is(d.Id, first));

		match.Status.Should().Be(HttpStatusCode.OK);
		takenElsewhere.Status.Should().Be(HttpStatusCode.BadRequest);
		matched.IncludedPaymentIds.Should().Equal("debt-out-2");
		matched.PaymentIds.Should().Equal("debt-out-2");
		matched.Paid.Should().Be(300m);
		unmatch.Status.Should().Be(HttpStatusCode.OK);
		unmatched.IncludedPaymentIds.Should().BeEmpty();
		unmatched.ExcludedPaymentIds.Should().Equal("debt-out-2");
		reset.Status.Should().Be(HttpStatusCode.OK);
		resetDebt.ExcludedPaymentIds.Should().BeEmpty();
	}

	/// <summary>Verifies a payment that does not exist answers 404 and money in answers 400 when matched or unmatched.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_MatchPayment_MustExistAndBeADebit()
	{
		using var client = fixture.CreateClient();
		var id = JsonSerializer.Deserialize<DecisionResponse>((await PostAsync(client, AddDebt("Payment Check Lender", iban: "BE99000011110003", plan: MonthlyPlan))).Text, ApiJson)!.Id!;

		var missing = await PostAsync(client, new DecisionRequest("matchRepayment", Id: id, Key: "debt-nothing"));
		var credit = await PostAsync(client, new DecisionRequest("matchRepayment", Id: id, Key: "debt-wages"));
		var creditUnmatch = await PostAsync(client, new DecisionRequest("unmatchRepayment", Id: id, Key: "debt-wages"));
		var missingUnmatch = await PostAsync(client, new DecisionRequest("unmatchRepayment", Id: id, Key: "debt-nothing"));

		missing.Status.Should().Be(HttpStatusCode.NotFound);
		credit.Status.Should().Be(HttpStatusCode.BadRequest);
		creditUnmatch.Status.Should().Be(HttpStatusCode.BadRequest);
		missingUnmatch.Status.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>Verifies a payment matched to a debt is no spending in the workspace totals.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Debts_MatchedPayment_FlaggedAsRepayment()
	{
		using var client = fixture.CreateClient();
		var id = JsonSerializer.Deserialize<DecisionResponse>((await PostAsync(client, AddDebt("Repayment Lender", iban: "BE99000011110004", plan: MonthlyPlan))).Text, ApiJson)!.Id!;

		await PostAsync(client, new DecisionRequest("matchRepayment", Id: id, Key: "debt-out-3"));

		var snapshot = await ReadSnapshotAsync(client);
		snapshot.Analysis.Items.Single(i => Is(i.Id, "debt-out-3")).Repayment.Should().BeTrue();
		snapshot.ToAnalysis().Items.Single(i => Is(i.T.Id.Value, "debt-out-3")).IsSpending.Should().BeFalse();
	}
	#endregion

	#region Private Helpers
	/// <summary>Compares two ids ordinally.</summary>
	private static bool Is(string? value, string expected) => string.Equals(value, expected, StringComparison.Ordinal);

	/// <summary>Builds a debt request with a plan.</summary>
	private static DebtRequest Planned(RepaymentPlanRequest plan) => new(DebtKind.PersonalLoan, "Friend", LenderIban, Plan: plan, BorrowedOn: Borrowed);

	/// <summary>Builds an addDebt request.</summary>
	private static DecisionRequest AddDebt(string lender = "Friend", string? received = null, RepaymentPlanRequest? plan = null, string? iban = null, decimal? principal = null) =>
		new("addDebt", Debt: new DebtRequest(DebtKind.PersonalLoan, lender, iban, received, principal, Plan: plan, BorrowedOn: received is null ? Borrowed : null));

	/// <summary>Posts a decision and returns the status and the answer text.</summary>
	private static async Task<(HttpStatusCode Status, string Text)> PostAsync(HttpClient client, DecisionRequest request)
	{
		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request).ConfigureAwait(false);
		return (response.StatusCode, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
	}

	/// <summary>Reads the workspace.</summary>
	private static async Task<WorkspaceSnapshot> ReadSnapshotAsync(HttpClient client) =>
		await client.GetFromJsonAsync<WorkspaceSnapshot>(new Uri("/api/v1/workspace", UriKind.Relative), ApiJson).ConfigureAwait(false)
			?? throw new InvalidOperationException("The workspace response must be present.");

	/// <summary>Reads the debts of the workspace.</summary>
	private static async Task<IReadOnlyList<DebtSnapshot>> ReadDebtsAsync(HttpClient client) => (await ReadSnapshotAsync(client).ConfigureAwait(false)).Analysis.Debts;

	/// <summary>Adds a loan payout of the test account.</summary>
	private static void AddCredit(KbcTrackerDbContext db, string id, decimal amount, BankRecord record) =>
		db.Transactions.Add(Txn.Rehydrate(new TxnId(id), "current", new DateOnly(2026, 10, 1), Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate("Debt Payer", null), "Money in", record));

	/// <summary>Adds a payment of the test account.</summary>
	private static void AddDebit(KbcTrackerDbContext db, string id, decimal amount) =>
		db.Transactions.Add(Txn.Rehydrate(new TxnId(id), "current", new DateOnly(2026, 10, 10), Money.Rehydrate(amount, "EUR"),
			Counterparty.Rehydrate("Debt Friend", LenderIban), "Payment", BankRecord.None));
	#endregion
}
