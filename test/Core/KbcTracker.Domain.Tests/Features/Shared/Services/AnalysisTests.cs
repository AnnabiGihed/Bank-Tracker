using System.Globalization;

namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins the behaviour of <see cref="Analysis"/> that the user relies on: classification by ISO bank
///              transaction code, card shop names, automatic categories by keyword, and fixed-payment suggestions
///              and matching.
///
///              Uses invented transactions from <see cref="BankJson"/> and a fixed reference date, so results do
///              not depend on the machine or the day the tests run.
/// </summary>
public sealed class AnalysisTests
{
	#region Test Infrastructure
	/// <summary>
	/// The fixed "today" of every analysis in this class.
	/// </summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>
	/// Creates the default categories with a few keywords, as the app does on first run.
	/// </summary>
	private static IList<Category> Categories() => CategoryDefaults.Create(new Dictionary<string, string[]>
	{
		["Groceries"] = ["DELHAIZE", "AD ", "CARREFOUR"],
		["Car"] = ["AUTOMOBILE"],
		["Subscriptions"] = ["YOUTUBE"],
		["Housing"] = ["LANDLORD"],
	});

	/// <summary>
	/// Runs the analysis on the given transactions with no user decisions unless some are given.
	/// </summary>
	private static Analysis Analyse(IEnumerable<Txn> txns, UserData? user = null) =>
		new([.. txns], Categories(), user ?? new UserData(), Today);

	/// <summary>
	/// Three months of a YouTube card subscription, a rent transfer and a loan instalment, with the labels
	/// changing slightly the way KBC's do.
	/// </summary>
	private static List<Txn> ThreeMonthsOfFixedPayments() =>
	[
		BankJson.Card("2026-08-12", 13.99m, "WL*GOOGLE YouTube IED02 R296 Dublin"),
		BankJson.Card("2026-09-14", 13.99m, "WL*GOOGLE YouTube IED02 R301 Dublin"),
		BankJson.Card("2026-10-13", 13.99m, "WL*GOOGLE YouTube IEUNKNOWN Dublin"),
		BankJson.TransferOut("2026-09-02", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.TransferOut("2026-10-01", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.TransferOut("2026-11-02", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.LoanInstalment("2026-09-07", 375.50m, "726-0000000-01"),
		BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01"),
		BankJson.LoanInstalment("2026-11-09", 375.50m, "726-0000000-01"),
	];
	#endregion

	#region Classification Tests
	/// <summary>
	/// Verifies that a card label is reduced to the shop name: processor prefix, country+postcode token, city and
	/// the "Payment with" tail are removed, and the series key is built from the shop words.
	/// </summary>
	[Fact]
	public void Classify_CardLabel_ShopNameAndCardSeriesKey()
	{
		var item = Analyse([BankJson.Card("2026-10-13", 13.99m, "WL*GOOGLE YouTube IED02 R296 Dublin")]).Items.Single();

		item.Kind.Should().Be(Kind.Card);
		item.Name.Should().Be("GOOGLE YouTube");
		item.SeriesKey.Should().Be("CARD:GOOGLE YOUTUBE");
	}

	/// <summary>
	/// Verifies that a <c>CSLN</c> instalment is a loan, categorised as such without asking the user.
	/// </summary>
	[Fact]
	public void Classify_LoanCode_LoanCategoryConfirmed()
	{
		var item = Analyse([BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01")]).Items.Single();

		item.Kind.Should().Be(Kind.Loan);
		item.Category.Id.Should().Be("loan");
		item.CategoryConfirmed.Should().BeTrue("Bank-certain kinds need no confirmation.");
		item.SeriesKey.Should().Be("LOAN:726-0000000-01");
	}

	/// <summary>
	/// Verifies that a transfer to the account holder's own name is an own transfer, not spending.
	/// </summary>
	[Fact]
	public void Classify_TransferToHolder_OwnTransfer()
	{
		var analysis = Analyse(
		[
			BankJson.Card("2026-10-10", 5m, "SHOP ONE BE1000 Bruxelles"),
			BankJson.TransferOut("2026-10-11", 200m, BankJson.Holder, "BE00999988887777"),
		]);

		var transfer = analysis.Items.Single(i => i.T.Amount == -200m);
		transfer.Kind.Should().Be(Kind.OwnTransferOut);
		transfer.Category.Type.Should().Be(CategoryType.Transfer);
	}

	/// <summary>
	/// Verifies that a loan payout (<c>RCDT/CAJT</c>) is not counted as income.
	/// </summary>
	[Fact]
	public void Classify_LoanPayout_NotIncome()
	{
		var item = Analyse([BankJson.TransferIn("2026-10-05", 10000m, "KBC BANK NV", "Loan payout", subCode: "CAJT")]).Items.Single();

		item.Kind.Should().Be(Kind.LoanReceived);
		item.IsIncome.Should().BeFalse();
	}
	#endregion

	#region Keyword Category Tests
	/// <summary>
	/// Verifies that keywords match only at the start of a word: "AD " finds an AD Delhaize shop but not shops
	/// whose name merely ends in "ad" (the Azad / Rad Huy regression).
	/// </summary>
	/// <param name="shopLabel">The card label before "Payment with".</param>
	/// <param name="expectedCategory">The expected category id.</param>
	[Theory]
	[InlineData("AD WOLUWE BE1200 Bruxelles", "groceries")]
	[InlineData("AZAD FR75002 PARIS", "other")]
	[InlineData("RAD HUY BE4537 CHAPON-SERAIN", "other")]
	public void Category_Keyword_MatchesOnlyAtWordStart(string shopLabel, string expectedCategory)
	{
		var item = Analyse([BankJson.Card("2026-10-20", 20m, shopLabel)]).Items.Single();

		item.Category.Id.Should().Be(expectedCategory, $"Shop label: {shopLabel}");
		item.CategoryConfirmed.Should().BeFalse("Keyword categories wait for the user's confirmation.");
	}

	/// <summary>
	/// Verifies that the longest matching keyword wins ("AUTOMOBILE" beats "CARREFOUR").
	/// </summary>
	[Fact]
	public void Category_SeveralKeywords_LongestWins()
	{
		var item = Analyse([BankJson.TransferOut("2026-11-09", 500m, "CARREFOUR WALLON AUTOMOBILE", "BE00444455556666")]).Items.Single();

		item.Category.Id.Should().Be("car");
	}
	#endregion

	#region Suggestion Tests
	/// <summary>
	/// Verifies that the three fixed payments the user accepted in real life are suggested from three months of
	/// history: the YouTube card subscription, the rent transfer and the loan instalment.
	/// </summary>
	[Fact]
	public void Suggestions_ThreeMonths_YouTubeRentAndLoanSuggested()
	{
		var suggestions = Analyse(ThreeMonthsOfFixedPayments()).Suggestions;

		suggestions.Should().HaveCount(3, string.Join(", ", suggestions.Select(s => $"{s.Name} {s.Amount}")));
		AssertSuggested(suggestions, 13.99m, Kind.Card, "Monthly");
		AssertSuggested(suggestions, 940m, Kind.Transfer, "Monthly");
		AssertSuggested(suggestions, 375.50m, Kind.Loan, "Monthly");
	}

	/// <summary>
	/// Verifies that card payments at one shop with very different amounts are not suggested as fixed.
	/// </summary>
	[Fact]
	public void Suggestions_IrregularAmounts_NotSuggested()
	{
		var suggestions = Analyse(
		[
			BankJson.Card("2026-08-10", 12.40m, "SHOP ONE BE1000 Bruxelles"),
			BankJson.Card("2026-09-10", 61.15m, "SHOP ONE BE1000 Bruxelles"),
			BankJson.Card("2026-10-10", 33.80m, "SHOP ONE BE1000 Bruxelles"),
		]).Suggestions;

		suggestions.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that a dismissed suggestion does not come back.
	/// </summary>
	[Fact]
	public void Suggestions_Dismissed_NotSuggestedAgain()
	{
		var txns = ThreeMonthsOfFixedPayments();
		var rent = Analyse(txns).Suggestions.Single(s => s.Amount == 940m);
		var user = new UserData { DismissedSuggestions = new HashSet<string>(StringComparer.Ordinal) { rent.Signature } };

		var suggestions = Analyse(txns, user).Suggestions;

		suggestions.Any(s => s.Amount == 940m).Should().BeFalse();
		suggestions.Should().HaveCount(2);
	}

	/// <summary>
	/// Verifies that an accepted suggestion becomes a fixed payment: its transactions are matched to it, it is no
	/// longer suggested, and it shows as paid in a month where it was paid.
	/// </summary>
	[Fact]
	public void Suggestions_Accepted_MatchedAndShownAsPaid()
	{
		var txns = ThreeMonthsOfFixedPayments();
		var youTube = Analyse(txns).Suggestions.Single(s => s.Amount == 13.99m);
		var user = new UserData { Recurring = [Analysis.ToDefinition(youTube)] };

		var analysis = Analyse(txns, user);

		analysis.Suggestions.Any(s => s.Amount == 13.99m).Should().BeFalse();
		var row = analysis.Fixed.Single();
		row.Payments.Should().HaveCount(3);
		row.StatusIn("2026-10", Today).Status.Should().Be(FixedStatus.Paid);
	}

	/// <summary>
	/// Verifies that a transfer repeating every three months is suggested as quarterly once it has been seen
	/// twice. This pins today's behaviour before the quarterly/yearly detection is reworked in step 4.
	/// </summary>
	[Fact]
	public void Suggestions_QuarterlyTransfer_SuggestedAsQuarterly()
	{
		var suggestions = Analyse(
		[
			BankJson.TransferOut("2026-05-15", 120m, "MOTOR INSURER", "BE00777788889999"),
			BankJson.TransferOut("2026-08-14", 120m, "MOTOR INSURER", "BE00777788889999"),
		]).Suggestions;

		suggestions.Single().Cadence.Should().Be("Quarterly");
	}

	/// <summary>
	/// Verifies that removing an accepted fixed payment keeps it dismissed: the signature stored from the fixed
	/// payment's series keys matches the suggestion's, even with accented keys whose ordinal and culture orders
	/// differ, and a signature stored in the old culture order is still honoured.
	/// </summary>
	[Fact]
	public void Suggestions_RemovedFixedPaymentWithAccentedKeys_StaysDismissed()
	{
		List<Txn> txns =
		[
			BankJson.Card("2026-09-05", 30m, "ÉCOLE DANSE BE1000 Bruxelles"),
			BankJson.Card("2026-10-05", 30m, "FOO ÉCOLE BE1000 Bruxelles"),
			BankJson.Card("2026-11-05", 30m, "ÉCOLE DANSE BE1000 Bruxelles"),
		];
		var suggestion = Analyse(txns).Suggestions.Single();
		suggestion.SeriesKeys.Should().HaveCount(2, "Both shops are one suggestion (same amount, shared word ÉCOLE).");
		var removed = Analysis.ToDefinition(suggestion);
		var ordinal = Analysis.Signature(removed.SeriesKeys);
		var cultureOrder = string.Join('|', removed.SeriesKeys.Order(StringComparer.Create(CultureInfo.GetCultureInfo("fr-BE"), ignoreCase: false)));
		cultureOrder.Should().NotBe(ordinal, "The keys must sort differently, or this test proves nothing.");

		var fromRemoval = new UserData { DismissedSuggestions = new HashSet<string>(StringComparer.Ordinal) { ordinal } };
		var legacy = new UserData { DismissedSuggestions = new HashSet<string>(StringComparer.Ordinal) { cultureOrder } };

		Analyse(txns, fromRemoval).Suggestions.Should().BeEmpty();
		Analyse(txns, legacy).Suggestions.Should().BeEmpty();
	}
	#endregion

	#region Category Override Tests
	/// <summary>
	/// Verifies the category order: a transaction's own category beats its payee's, and the payee's applies to the
	/// payee's other transactions, both confirmed.
	/// </summary>
	[Fact]
	public void Category_PayeeAndTransactionChoices_TransactionWinsOverPayee()
	{
		var first = BankJson.Card("2026-10-02", 20m, "SHOP ONE BE1000 Bruxelles");
		var second = BankJson.Card("2026-10-09", 30m, "SHOP ONE BE1000 Bruxelles");
		var key = Analyse([first, second]).Items[0].SeriesKey;
		var user = new UserData();
		user.Payees[key] = new PayeeAssignment { CategoryId = "groceries", Confirmed = true };
		user.TxnCategories[second.Id] = "car";

		var items = Analyse([first, second], user).Items;

		var firstItem = items.Single(i => string.Equals(i.T.Id, first.Id, StringComparison.Ordinal));
		var secondItem = items.Single(i => string.Equals(i.T.Id, second.Id, StringComparison.Ordinal));
		firstItem.Category.Id.Should().Be("groceries");
		secondItem.Category.Id.Should().Be("car");
		firstItem.CategoryConfirmed.Should().BeTrue();
		secondItem.CategoryConfirmed.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that payments matched to an accepted fixed payment take its category instead of the automatic one.
	/// </summary>
	[Fact]
	public void Category_AcceptedFixedPayment_ItsCategoryBeatsKeyword()
	{
		var txns = ThreeMonthsOfFixedPayments();
		var rent = Analysis.ToDefinition(Analyse(txns).Suggestions.Single(s => s.Amount == 940m));
		rent.CategoryId = "bills";

		var items = Analyse(txns, new UserData { Recurring = [rent] }).Items.Where(i => i.T.Amount == -940m).ToList();

		items.Should().HaveCount(3);
		items.TrueForAll(i => string.Equals(i.Category.Id, "bills", StringComparison.Ordinal) && i.CategoryConfirmed && i.Recurring == rent).Should().BeTrue();
	}
	#endregion

	#region Fixed Status Tests
	/// <summary>
	/// Verifies the state of an accepted monthly payment in a month without payment: due until 4 days after its
	/// expected date, late after that, and not started before its first payment.
	/// </summary>
	[Fact]
	public void StatusIn_MonthlyNotPaidYet_DueThenLateAndNotStartedBefore()
	{
		var txns = ThreeMonthsOfFixedPayments();
		var youTube = Analysis.ToDefinition(Analyse(txns).Suggestions.Single(s => s.Amount == 13.99m));
		var row = Analyse(txns, new UserData { Recurring = [youTube] }).Fixed.Single();

		var due = row.StatusIn("2026-11", new DateOnly(2026, 11, 9));
		var late = row.StatusIn("2026-11", new DateOnly(2026, 11, 20));

		due.Status.Should().Be(FixedStatus.Due);
		due.Date.Should().Be(new DateOnly(2026, 11, 12), "Expected 30 days after the last payment (13 October).");
		late.Status.Should().Be(FixedStatus.Late);
		row.StatusIn("2026-07", Today).Status.Should().Be(FixedStatus.NotStarted);
	}
	#endregion

	#region Month Summary Tests
	/// <summary>
	/// Verifies a month's totals: income, fixed spending (matched to accepted fixed payments), variable spending,
	/// what is left, and spending per category.
	/// </summary>
	[Fact]
	public void MonthData_AcceptedYouTube_SplitsFixedAndVariableSpending()
	{
		var txns = ThreeMonthsOfFixedPayments();
		txns.Add(BankJson.TransferIn("2026-10-30", 2500m, "ACME PAYROLL", "Salary October"));
		var youTube = Analysis.ToDefinition(Analyse(txns).Suggestions.Single(s => s.Amount == 13.99m));

		var october = Analyse(txns, new UserData { Recurring = [youTube] }).MonthData("2026-10");

		october.Income.Should().Be(2500m);
		october.Fixed.Should().Be(13.99m);
		october.Variable.Should().Be(940m + 375.50m);
		october.Net.Should().Be(2500m - 13.99m - 940m - 375.50m);
		october.ByCategory["housing"].Should().Be(940m);
	}

	/// <summary>
	/// Verifies that a month without transactions gives an all-zero summary.
	/// </summary>
	[Fact]
	public void MonthData_UnknownMonth_AllZero()
	{
		var summary = Analyse(ThreeMonthsOfFixedPayments()).MonthData("2020-01");

		summary.Spent.Should().Be(0m);
		summary.Income.Should().Be(0m);
	}
	#endregion

	#region Review Queue Tests
	/// <summary>
	/// Verifies that the review queue holds one row per payee with an unconfirmed automatic category, largest
	/// total first, and that confirming a payee removes it. The loan is bank-certain and never listed.
	/// </summary>
	[Fact]
	public void Review_UnconfirmedPayees_LargestFirstAndConfirmRemoves()
	{
		var txns = ThreeMonthsOfFixedPayments();
		var review = Analyse(txns).Review;

		review.Should().HaveCount(2, "Rent and YouTube; the loan is confirmed by its bank code.");
		review[0].Total.Should().Be(-2820m);
		review[1].Count.Should().Be(3, "The three YouTube labels are one payee.");

		var user = new UserData();
		user.Payees[review[0].SeriesKey] = new PayeeAssignment { CategoryId = review[0].Category.Id, Confirmed = true };
		Analyse(txns, user).Review.Should().HaveCount(1);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Asserts that exactly one suggestion has the given amount, kind and cadence.
	/// </summary>
	private static void AssertSuggested(IReadOnlyList<Suggestion> suggestions, decimal amount, Kind kind, string cadence)
	{
		var match = suggestions.SingleOrDefault(s => s.Amount == amount);
		match.Should().NotBeNull($"No suggestion of {amount} €.");
		match.Kind.Should().Be(kind, $"Kind of the {amount} € suggestion.");
		match.Cadence.Should().Be(cadence, $"Cadence of the {amount} € suggestion.");
	}
	#endregion
}
