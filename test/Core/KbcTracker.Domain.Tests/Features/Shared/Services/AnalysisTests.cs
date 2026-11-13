using System.Globalization;

namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins the behaviour of <see cref="Analysis"/> that the user relies on: classification by ISO bank
///              transaction code, card shop names, automatic categories by keyword, and fixed-payment suggestions
///              and matching, and how split payments count by part.
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
		new([.. txns], Categories(), user ?? UserData.Empty(), Today);

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
		item.Category.Id.Value.Should().Be("loan");
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
		item.Category.Id.Should().Be(CategoryId.LoanReceived);
		item.Category.Type.Should().Be(CategoryType.Transfer);
		item.IsSpending.Should().BeFalse();
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

		item.Category.Id.Value.Should().Be(expectedCategory, $"Shop label: {shopLabel}");
		item.CategoryConfirmed.Should().BeFalse("Keyword categories wait for the user's confirmation.");
	}

	/// <summary>
	/// Verifies that the longest matching keyword wins ("AUTOMOBILE" beats "CARREFOUR").
	/// </summary>
	[Fact]
	public void Category_SeveralKeywords_LongestWins()
	{
		var item = Analyse([BankJson.TransferOut("2026-11-09", 500m, "CARREFOUR WALLON AUTOMOBILE", "BE00444455556666")]).Items.Single();

		item.Category.Id.Value.Should().Be("car");
	}
	#endregion

	#region Category Tree Tests
	/// <summary>
	/// Verifies that when two keywords are equally long the deeper category wins, whatever the list order.
	/// </summary>
	/// <param name="parentFirst">Whether the parent comes first in the category list.</param>
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void KeywordCategory_EqualKeywordsInParentAndChild_DeeperCategoryWins(bool parentFirst)
	{
		var other = Category.Create(CategoryId.Other, "Other", CategoryType.Expense, "#123456", [], builtIn: true).Value;
		var parent = Category.Create(new CategoryId("parent"), "Parent", CategoryType.Expense, "#123456", ["SHOPPY"], builtIn: false).Value;
		var child = Category.Create(new CategoryId("child"), "Child", CategoryType.Expense, "#123456", ["SHOPPY"], builtIn: false, new CategoryId("parent")).Value;
		IEnumerable<Category> categories = parentFirst ? [other, parent, child] : [other, child, parent];

		var analysis = new Analysis([BankJson.Card("2026-10-10", 5m, "SHOPPY BE1000 Bruxelles")], categories, UserData.Empty(), Today);

		analysis.Items.Single().Category.Id.Value.Should().Be("child");
	}

	/// <summary>
	/// Verifies that a STIB payment lands on "Public and paid transport", not on a mode the payment does not name.
	/// </summary>
	[Fact]
	public void KeywordCategory_Stib_PublicTransportNotALeaf()
	{
		var analysis = new Analysis([BankJson.Card("2026-10-10", 2.5m, "STIB BE1000 Bruxelles")], CategoryDefaults.CreateDefaults("BE"), UserData.Empty(), Today);

		analysis.Items.Single().Category.Id.Value.Should().Be("transport-public");
		analysis.Tree.Children("transport-public").Should().NotBeEmpty();
	}

	/// <summary>
	/// Verifies that the month's per-category amounts stay per assigned node and that rolling them up through the
	/// tree gives each root the sum of its descendants, adding up to the month's spending.
	/// </summary>
	[Fact]
	public void BuildMonth_ByCategoryAndRollUp_Consistent()
	{
		var analysis = new Analysis(
		[
			BankJson.Card("2026-10-10", 2.5m, "STIB BE1000 Bruxelles"),
			BankJson.Card("2026-10-11", 40m, "SHELL BE1000 Bruxelles"),
			BankJson.Card("2026-10-12", 7m, "LIDL BE1000 Bruxelles"),
		], CategoryDefaults.CreateDefaults("BE"), UserData.Empty(), Today);

		var october = analysis.MonthData("2026-10");
		var rolled = analysis.Tree.RollUp(october.ByCategory);

		october.ByCategory["transport-public"].Should().Be(2.5m);
		october.ByCategory["car-fuel"].Should().Be(40m);
		rolled["transport"].Should().Be(42.5m);
		rolled["car"].Should().Be(40m);
		analysis.Tree.Roots().Where(r => rolled.ContainsKey(r.Id.Value)).Sum(r => rolled[r.Id.Value]).Should().Be(october.ByCategory.Values.Sum());
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
		var user = UserData.Empty();
		user.DismissSuggestion(rent.Signature);

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
		var user = Accepting(Define(youTube));

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
		var removed = Define(suggestion);
		var ordinal = PayeeMatch.SignatureOf(removed.SeriesKeys);
		var cultureOrder = string.Join('|', removed.SeriesKeys.Order(StringComparer.Create(CultureInfo.GetCultureInfo("fr-BE"), ignoreCase: false)));
		cultureOrder.Should().NotBe(ordinal, "The keys must sort differently, or this test proves nothing.");

		var fromRemoval = Accepting(removed);
		fromRemoval.RemoveFixedPayment(removed.Id).IsSuccess.Should().BeTrue();
		var legacy = UserData.Empty();
		legacy.DismissSuggestion(cultureOrder);

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
		var user = UserData.Empty();
		user.AssignPayeeCategory(key, new CategoryId("groceries"));
		user.AssignTransactionCategory(second.Id, new CategoryId("car"));

		var items = Analyse([first, second], user).Items;

		var firstItem = items.Single(i => i.T.Id.Equals(first.Id));
		var secondItem = items.Single(i => i.T.Id.Equals(second.Id));
		firstItem.Category.Id.Value.Should().Be("groceries");
		secondItem.Category.Id.Value.Should().Be("car");
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
		var rent = Define(Analyse(txns).Suggestions.Single(s => s.Amount == 940m));
		var user = Accepting(rent);
		user.UpdateFixedPayment(rent.Id, name: null, new CategoryId("bills"), amount: null, cadenceDays: null).IsSuccess.Should().BeTrue();

		var items = Analyse(txns, user).Items.Where(i => i.T.Amount == -940m).ToList();

		items.Should().HaveCount(3);
		items.TrueForAll(i => string.Equals(i.Category.Id.Value, "bills", StringComparison.Ordinal) && i.CategoryConfirmed && i.Recurring == rent).Should().BeTrue();
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
		var youTube = Define(Analyse(txns).Suggestions.Single(s => s.Amount == 13.99m));
		var row = Analyse(txns, Accepting(youTube)).Fixed.Single();

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
		var youTube = Define(Analyse(txns).Suggestions.Single(s => s.Amount == 13.99m));

		var october = Analyse(txns, Accepting(youTube)).MonthData("2026-10");

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

	/// <summary>
	/// Verifies that income is earnings only: a salary, another income and a refund are summed apart, and the loan
	/// payout is its own figure; refunds and loan money are in no total, so what is left ignores them.
	/// </summary>
	[Fact]
	public void MonthData_SalaryRefundAndLoan_EarningsRefundsAndLoanApart()
	{
		var payroll = BankJson.TransferIn("2026-10-25", 2500m, "ACME PAYROLL", "Salary October");
		var user = UserData.Empty();
		user.AssignTransactionCategory(payroll.Id, new CategoryId("income-salary"));
		var analysis = AnalyseDefaults(
		[
			payroll,
			BankJson.TransferIn("2026-10-26", 100m, "FRIEND ONE", "Thanks"),
			BankJson.CardRefund("2026-10-27", 30m, "SHOPPY BE1000 Bruxelles"),
			BankJson.TransferIn("2026-10-05", 10000m, "KBC BANK NV", "Loan payout", subCode: "CAJT"),
			BankJson.Card("2026-10-10", 50m, "SHOPPY BE1000 Bruxelles"),
		], user);

		var october = analysis.MonthData("2026-10");

		october.Income.Should().Be(2600m);
		october.Salary.Should().Be(2500m);
		october.Refunds.Should().Be(30m);
		october.LoanReceived.Should().Be(10000m);
		october.Spent.Should().Be(50m);
		october.Net.Should().Be(2600m - 50m);
	}

	/// <summary>
	/// Verifies that a credit the owner moved into the Refunds category counts as a refund, not as earnings.
	/// </summary>
	[Fact]
	public void MonthData_CreditAssignedToRefunds_NotEarnings()
	{
		var credit = BankJson.TransferIn("2026-10-25", 40m, "FRIEND ONE", "Money back");
		var user = UserData.Empty();
		user.AssignTransactionCategory(credit.Id, new CategoryId("refunds"));

		var october = AnalyseDefaults([credit], user).MonthData("2026-10");

		october.Income.Should().Be(0m);
		october.Refunds.Should().Be(40m);
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

		var user = UserData.Empty();
		user.AssignPayeeCategory(review[0].SeriesKey, review[0].Category.Id);
		Analyse(txns, user).Review.Should().HaveCount(1);
	}
	#endregion

	#region Split Tests
	/// <summary>
	/// Runs the analysis with the default category tree, where a supermarket payment lands on food.
	/// </summary>
	private static Analysis AnalyseDefaults(IEnumerable<Txn> txns, UserData user) =>
		new([.. txns], CategoryDefaults.CreateDefaults("BE"), user, Today);

	/// <summary>
	/// Splits a payment as the application does, after checking the parts add up.
	/// </summary>
	private static void Split(UserData user, Txn txn, params SplitLine[] lines)
	{
		var split = PaymentSplit.Create(txn.Amount, lines);
		split.IsSuccess.Should().BeTrue();
		user.SplitPayment(txn.Id, split.Value);
	}

	/// <summary>
	/// Verifies that the parts of a split payment feed the totals by category, the payment stays one spending item
	/// confirmed by the split, and the payment's own category is the one it has without the split.
	/// </summary>
	[Fact]
	public void BuildMonth_SplitPayment_ByCategoryFollowsParts()
	{
		var shop = BankJson.Card("2026-10-10", 84.30m, "DELHAIZE BE1000 Bruxelles");
		var user = UserData.Empty();
		Split(user, shop, new SplitLine(50m, new CategoryId("food-meat"), "Meat"), new SplitLine(34.30m, new CategoryId("care-toiletries"), null));

		var analysis = AnalyseDefaults([shop], user);

		var item = analysis.Items.Single();
		var october = analysis.MonthData("2026-10");
		item.IsSplit.Should().BeTrue();
		item.IsSpending.Should().BeTrue();
		item.CategoryConfirmed.Should().BeTrue();
		item.Category.Id.Value.Should().NotBe("food-meat", "The payment's own category ignores the split.");
		item.Parts.Select(p => (p.Category.Id.Value, p.Amount, p.Label)).Should().Equal(("food-meat", -50m, "Meat"), ("care-toiletries", -34.30m, (string?)null));
		october.ByCategory.Should().Equal(new Dictionary<string, decimal> { ["food-meat"] = 50m, ["care-toiletries"] = 34.30m });
		october.Variable.Should().Be(84.30m);
		analysis.Review.Should().BeEmpty("A split payment counts as confirmed.");
	}

	/// <summary>
	/// Verifies that an unsplit payment has one part with its whole amount and its category.
	/// </summary>
	[Fact]
	public void Parts_UnsplitPayment_OnePartWithWholeAmount()
	{
		var item = AnalyseDefaults([BankJson.Card("2026-10-10", 12m, "DELHAIZE BE1000 Bruxelles")], UserData.Empty()).Items.Single();

		item.IsSplit.Should().BeFalse();
		item.Parts.Should().ContainSingle().Which.Should().Be(new TxnPart(item.Category, -12m, null));
	}

	/// <summary>
	/// Verifies that a split wins over a category chosen for the payment alone, which comes back when the split is
	/// removed.
	/// </summary>
	[Fact]
	public void Split_WithTransactionCategory_SplitWinsUntilRemoved()
	{
		var shop = BankJson.Card("2026-10-10", 20m, "DELHAIZE BE1000 Bruxelles");
		var user = UserData.Empty();
		user.AssignTransactionCategory(shop.Id, new CategoryId("car-fuel"));
		Split(user, shop, new SplitLine(5m, new CategoryId("food-meat"), null), new SplitLine(15m, new CategoryId("food-bread-cereals"), null));

		var split = AnalyseDefaults([shop], user).MonthData("2026-10").ByCategory;
		user.RemoveSplit(shop.Id);
		var restored = AnalyseDefaults([shop], user).MonthData("2026-10").ByCategory;

		split.Should().NotContainKey("car-fuel");
		split.Values.Sum().Should().Be(20m);
		restored.Should().Equal(new Dictionary<string, decimal> { ["car-fuel"] = 20m });
	}

	/// <summary>
	/// Verifies that splitting one payment does not teach the payee's other payments its parts' categories.
	/// </summary>
	[Fact]
	public void Split_OnePayment_PayeeCategoryUnchanged()
	{
		var first = BankJson.Card("2026-10-02", 20m, "DELHAIZE BE1000 Bruxelles");
		var second = BankJson.Card("2026-10-09", 30m, "DELHAIZE BE1000 Bruxelles");
		var user = UserData.Empty();
		user.AssignPayeeCategory(Analyse([first]).Items.Single().SeriesKey, new CategoryId("groceries"));
		Split(user, second, new SplitLine(10m, new CategoryId("car"), null), new SplitLine(20m, new CategoryId("housing"), null));

		var items = Analyse([first, second], user).Items;

		items.Single(i => i.T.Id.Equals(first.Id)).Category.Id.Value.Should().Be("groceries");
		items.Single(i => i.T.Id.Equals(second.Id)).Category.Id.Value.Should().Be("groceries", "The unsplit category stays the payee's.");
	}

	/// <summary>
	/// Verifies that the parts of a refund reduce spending in their categories.
	/// </summary>
	[Fact]
	public void BuildMonth_SplitRefund_PartsReduceSpending()
	{
		var spend = BankJson.Card("2026-10-02", 100m, "DELHAIZE BE1000 Bruxelles");
		var refund = BankJson.TransferIn("2026-10-05", 30m, "DELHAIZE", "Return");
		var user = UserData.Empty();
		user.AssignTransactionCategory(refund.Id, new CategoryId("food-meat"));
		Split(user, refund, new SplitLine(10m, new CategoryId("food-meat"), null), new SplitLine(20m, new CategoryId("care-toiletries"), null));

		var october = AnalyseDefaults([spend, refund], user).MonthData("2026-10");

		october.ByCategory["food-meat"].Should().Be(-10m);
		october.ByCategory["care-toiletries"].Should().Be(-20m);
		october.Variable.Should().Be(70m);
	}

	/// <summary>
	/// Verifies that a stored split that no longer adds up gets a "Difference" part on the payment's own category,
	/// so the totals still equal the bank amount.
	/// </summary>
	[Fact]
	public void SplitParts_SumDiffersFromPayment_DifferencePartKeepsTotal()
	{
		var shop = BankJson.Card("2026-10-10", 100m, "DELHAIZE BE1000 Bruxelles");
		var user = UserData.Empty();
		user.SplitPayment(shop.Id, PaymentSplit.Rehydrate([new SplitLine(50m, new CategoryId("food-meat"), null), new SplitLine(34.30m, new CategoryId("care-toiletries"), null)]));

		var analysis = AnalyseDefaults([shop], user);

		var item = analysis.Items.Single();
		var difference = item.Parts[^1];
		difference.Label.Should().Be("Difference");
		difference.Amount.Should().Be(-15.70m);
		difference.Category.Should().Be(item.Category);
		item.Parts.Sum(p => p.Amount).Should().Be(-100m);
		analysis.MonthData("2026-10").ByCategory.Values.Sum().Should().Be(100m);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Turns a suggestion into a fixed payment, as accepting it in the UI does.
	/// </summary>
	private static RecurringPayment Define(Suggestion suggestion)
	{
		var definition = Analysis.ToDefinition(suggestion, new RecurringPaymentId("fixed-" + suggestion.Amount.ToString(CultureInfo.InvariantCulture)), new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero));
		definition.IsSuccess.Should().BeTrue();
		return definition.Value;
	}

	/// <summary>
	/// Creates the decisions of a user who accepted the given fixed payments.
	/// </summary>
	private static UserData Accepting(params RecurringPayment[] payments)
	{
		var user = UserData.Empty();
		foreach (var payment in payments)
		{
			user.AcceptFixedPayment(payment);
		}

		return user;
	}

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
