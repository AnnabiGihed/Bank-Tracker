namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins how <see cref="Analysis"/> tells money in apart: loan payouts, refunds, salary and plain income,
///              in that priority, with own transfers and the user's decisions winning over all of it, and that
///              <see cref="Analysis.Behind"/> lists exactly the payments the month's figures add up.
///
///              Uses invented names, IBANs and remittances shaped like KBC's.
/// </summary>
public sealed class IncomingClassificationTests
{
	#region Test Infrastructure
	/// <summary>
	/// The fixed "today" of every analysis in this class.
	/// </summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>
	/// The remittance of a Belgian payroll transfer, with its employer and employee tokens.
	/// </summary>
	private const string PayrollRemittance = "ACME NV Credit transfer /A/ EMPLOYER: 0000001 EMPLOYEE: 0000022 001 DATE: 29/10/2026";

	/// <summary>
	/// Runs the analysis with the default category tree and a card payment that makes <see cref="BankJson.Holder"/>
	/// the account holder.
	/// </summary>
	private static Analysis Analyse(UserData? user, params Txn[] txns) =>
		new([BankJson.Card("2026-01-05", 5m, "SHOP ONE BE1000 Bruxelles"), .. txns], CategoryDefaults.CreateDefaults("BE"), user ?? UserData.Empty(), Today);

	/// <summary>
	/// Runs the analysis without user decisions.
	/// </summary>
	private static Analysis Analyse(params Txn[] txns) => Analyse(null, txns);

	/// <summary>
	/// Gets the one analysed credit of the given amount.
	/// </summary>
	private static TxnInfo Credit(Analysis analysis, decimal amount) => analysis.Items.Single(i => i.T.Amount == amount);
	#endregion

	#region Salary Tests
	/// <summary>
	/// Verifies that the employer and employee tokens of a payroll transfer make it Salary, as a suggestion.
	/// </summary>
	[Fact]
	public void Classify_PayrollMarkers_SalarySuggestion()
	{
		var item = Credit(Analyse(BankJson.TransferIn("2026-10-29", 2500m, "ACME NV", PayrollRemittance)), 2500m);

		item.Kind.Should().Be(Kind.Income);
		item.Category.Id.Value.Should().Be("income-salary");
		item.CategoryConfirmed.Should().BeFalse("Salary is a suggestion the owner confirms.");
	}

	/// <summary>
	/// Verifies that a pay word at the start of a word makes a credit Salary, and that look-alikes do not.
	/// </summary>
	/// <param name="remittance">The remittance text.</param>
	/// <param name="expected">The expected category id.</param>
	[Theory]
	[InlineData("Salary October", "income-salary")]
	[InlineData("SALAIRE OCTOBRE", "income-salary")]
	[InlineData("LOON OKTOBER", "income-salary")]
	[InlineData("Paiement de la facture", "income")]
	[InlineData("Only an EMPLOYER: 0000001 here", "income")]
	public void Classify_PayWords_SalaryOnlyForPayWords(string remittance, string expected) =>
		Credit(Analyse(BankJson.TransferIn("2026-10-29", 2500m, "ACME NV", remittance)), 2500m).Category.Id.Value.Should().Be(expected);

	/// <summary>
	/// Verifies that a payer who credits the account about monthly is Salary without any marker, and that a payer who
	/// paid once is not.
	/// </summary>
	[Fact]
	public void Classify_SamePayerAboutMonthly_Salary()
	{
		var analysis = Analyse(
			BankJson.TransferIn("2026-09-28", 1800m, "WORKPLACE BV", "Transfer"),
			BankJson.TransferIn("2026-10-29", 1801m, "WORKPLACE BV", "Transfer"),
			BankJson.TransferIn("2026-10-12", 60m, "A FRIEND", "Thanks"));

		Credit(analysis, 1800m).Category.Id.Value.Should().Be("income-salary");
		Credit(analysis, 1801m).Category.Id.Value.Should().Be("income-salary");
		Credit(analysis, 60m).Category.Id.Value.Should().Be("income");
	}

	/// <summary>
	/// Verifies that payroll markers win over the paid-before rule: an employer the account once paid still pays Salary.
	/// </summary>
	[Fact]
	public void Classify_PayrollFromPaidPayer_Salary()
	{
		var analysis = Analyse(
			BankJson.TransferOut("2026-09-01", 3000m, "ACME NV", "BE00777788889999"),
			BankJson.TransferIn("2026-10-29", 2500m, "ACME NV", PayrollRemittance));

		Credit(analysis, 2500m).Kind.Should().Be(Kind.Income);
		Credit(analysis, 2500m).Category.Id.Value.Should().Be("income-salary");
	}
	#endregion

	#region Refund Tests
	/// <summary>
	/// Verifies that refund words make a credit a Refund, and that "refus prêt" is a refund although it holds a loan word.
	/// </summary>
	/// <param name="remittance">The remittance text.</param>
	[Theory]
	[InlineData("REMB AC VN SUITE REFUS PRET")]
	[InlineData("Remboursement facture 123")]
	[InlineData("Terugbetaling aankoop")]
	[InlineData("Refund order 77")]
	public void Classify_RefundWords_Refund(string remittance)
	{
		var item = Credit(Analyse(BankJson.TransferIn("2026-10-20", 700m, "CAR DEALER SA", remittance)), 700m);

		item.Kind.Should().Be(Kind.Refund);
		item.Category.Id.Value.Should().Be("refunds");
		item.CategoryConfirmed.Should().BeFalse();
	}

	/// <summary>
	/// Verifies that a credit from a counterparty the account paid before is a Refund, by IBAN or by name, and that a
	/// credit from before the first payment is not.
	/// </summary>
	[Fact]
	public void Classify_CreditFromPaidCounterparty_Refund()
	{
		var analysis = Analyse(
			BankJson.TransferOut("2026-09-01", 300m, "SHOP TWO", "BE00555566667777"),
			BankJson.TransferIn("2026-10-02", 120m, "SHOP TWO LTD", "Credit note", debtorIban: "BE00555566667777"),
			BankJson.TransferOut("2026-09-03", 90m, "SHOP THREE", "BE00111100002222"),
			BankJson.TransferIn("2026-10-05", 45m, "Shop Three", "Credit note"),
			BankJson.TransferIn("2026-08-01", 33m, "SHOP FOUR", "Early"),
			BankJson.TransferOut("2026-09-09", 10m, "SHOP FOUR", "BE00999900001111"));

		Credit(analysis, 120m).Kind.Should().Be(Kind.Refund);
		Credit(analysis, 45m).Kind.Should().Be(Kind.Refund);
		Credit(analysis, 33m).Kind.Should().Be(Kind.Income);
	}

	/// <summary>
	/// Verifies that a credit larger than anything the account ever paid the payer is no refund.
	/// </summary>
	[Fact]
	public void Classify_CreditLargerThanEarlierDebits_NotRefund()
	{
		var analysis = Analyse(
			BankJson.TransferOut("2026-09-01", 100m, "SHOP TWO", "BE00555566667777"),
			BankJson.TransferIn("2026-10-02", 500m, "SHOP TWO", "Thanks", debtorIban: "BE00555566667777"));

		Credit(analysis, 500m).Kind.Should().Be(Kind.Income);
	}

	/// <summary>
	/// Verifies that a refund-word credit without a bank code (old JSON import) keeps its payer's FROM: series.
	/// </summary>
	[Fact]
	public void Classify_NullCodeRefund_SeriesByPayer()
	{
		var credit = Txn.Rehydrate(new TxnId("nocode"), BankJson.AccountUid, new DateOnly(2026, 10, 20), Money.Rehydrate(80m, "EUR"),
			Counterparty.Rehydrate("SHOP FIVE", null), "Refund order 12", new BankRecord(null, null));

		var item = Credit(Analyse(credit), 80m);

		item.Kind.Should().Be(Kind.Refund);
		item.SeriesKey.Should().StartWith("FROM:");
	}

	/// <summary>
	/// Verifies that a transfer refund is grouped by its payer, not as a card shop.
	/// </summary>
	[Fact]
	public void Classify_TransferRefund_SeriesByPayer()
	{
		var item = Credit(Analyse(BankJson.TransferIn("2026-10-20", 700m, "CAR DEALER SA", "REMB", debtorIban: "BE00123412341234")), 700m);

		item.SeriesKey.Should().Be("FROM:BE00123412341234");
	}
	#endregion

	#region Loan Tests
	/// <summary>
	/// Verifies that loan words make a credit a loan payout as a suggestion, while the bank code (<c>CAJT</c> or the
	/// loan domain) makes it certain, and that look-alike words do not match.
	/// </summary>
	[Fact]
	public void Classify_LoanWordsAndCodes_LoanReceived()
	{
		var domain = Txn.Rehydrate(new TxnId("ldas"), BankJson.AccountUid, new DateOnly(2026, 10, 4), Money.Rehydrate(900m, "EUR"),
			Counterparty.Rehydrate("SOME LENDER", null), "Payout", new BankRecord("LDAS", null));
		var analysis = new Analysis(
			[BankJson.Card("2026-01-05", 5m, "SHOP ONE BE1000 Bruxelles"),
				BankJson.TransferIn("2026-10-05", 5000m, "CREDIT BANK", "Payment Instalment loan 420 UITBETALING LENING", "CAJT"),
				BankJson.TransferIn("2026-10-06", 4000m, "CREDIT BANK", "Prêt personnel"),
				BankJson.TransferIn("2026-10-07", 15m, "A FRIEND", "Pretty gift"),
				domain],
			CategoryDefaults.CreateDefaults("BE"), UserData.Empty(), Today);

		Credit(analysis, 5000m).Kind.Should().Be(Kind.LoanReceived);
		Credit(analysis, 5000m).CategoryConfirmed.Should().BeTrue("A loan code makes it certain.");
		Credit(analysis, 4000m).Kind.Should().Be(Kind.LoanReceived);
		Credit(analysis, 4000m).CategoryConfirmed.Should().BeFalse("Loan words only suggest it.");
		Credit(analysis, 900m).Kind.Should().Be(Kind.LoanReceived);
		Credit(analysis, 900m).CategoryConfirmed.Should().BeTrue();
		Credit(analysis, 15m).Kind.Should().Be(Kind.Income);
	}

	/// <summary>
	/// Verifies that a loan payout suggested only by words keeps its payer's FROM: series, so payee decisions still
	/// apply and lenders stay apart, while a bank-certain payout shares the LOANIN series.
	/// </summary>
	[Fact]
	public void Classify_LoanWordsCredit_KeepsFromSeries()
	{
		var analysis = Analyse(
			BankJson.TransferIn("2026-10-06", 4000m, "CREDIT BANK", "Prêt personnel", debtorIban: "BE00123412341234"),
			BankJson.TransferIn("2026-10-05", 5000m, "CREDIT BANK", "Payout", "CAJT", "BE00123412341234"));

		Credit(analysis, 4000m).SeriesKey.Should().Be("FROM:BE00123412341234");
		Credit(analysis, 5000m).SeriesKey.Should().Be("LOANIN");
	}

	/// <summary>
	/// Verifies that a credit from a counterparty the account repays a loan to is a loan payout, even when the account
	/// paid it before (loan evidence beats the paid-before refund rule).
	/// </summary>
	[Fact]
	public void Classify_CreditFromLender_LoanReceived()
	{
		var analysis = Analyse(
			BankJson.LoanInstalment("2026-09-07", 375.50m, "726-0000000-01"),
			BankJson.TransferIn("2026-10-10", 8000m, "KBC BANK NV", "Money"));

		Credit(analysis, 8000m).Kind.Should().Be(Kind.LoanReceived);
	}
	#endregion

	#region Priority Tests
	/// <summary>
	/// Verifies that a transfer from the account holder's own name stays an own transfer whatever its text says.
	/// </summary>
	[Fact]
	public void Classify_OwnTransferWithRefundAndSalaryWords_OwnTransfer()
	{
		var item = Credit(Analyse(BankJson.TransferIn("2026-10-20", 400m, BankJson.Holder, "Refund salary loan " + PayrollRemittance)), 400m);

		item.Kind.Should().Be(Kind.OwnTransferIn);
		item.Category.Id.Value.Should().Be("own-transfers");
	}

	/// <summary>
	/// Verifies that the owner's decision per transaction and per payee wins over the automatic category.
	/// </summary>
	[Fact]
	public void Classify_UserDecision_WinsOverAutomatic()
	{
		var salary = BankJson.TransferIn("2026-10-29", 2500m, "ACME NV", PayrollRemittance);
		var refund = BankJson.TransferIn("2026-10-20", 700m, "CAR DEALER SA", "REMB", debtorIban: "BE00123412341234");
		var first = Analyse(salary, refund);
		var user = UserData.Empty();
		user.AssignTransactionCategory(salary.Id, new CategoryId("income-other"));
		user.ReassignPayee(Credit(first, 700m).SeriesKey, new CategoryId("income-other"), confirmed: true);

		var analysis = Analyse(user, salary, refund);

		Credit(analysis, 2500m).Category.Id.Value.Should().Be("income-other");
		Credit(analysis, 700m).Category.Id.Value.Should().Be("income-other");
		Credit(analysis, 700m).CategoryConfirmed.Should().BeTrue();
	}
	#endregion

	#region Figure Tests
	/// <summary>
	/// Verifies that the payments behind each figure sum exactly to the figure, and that earnings, refunds and loan
	/// money do not overlap.
	/// </summary>
	[Fact]
	public void Behind_EachFigure_SumsToTheMonthSummary()
	{
		var analysis = Analyse(
			BankJson.TransferIn("2026-10-29", 2500m, "ACME NV", PayrollRemittance),
			BankJson.TransferIn("2026-10-12", 60m, "A FRIEND", "Thanks"),
			BankJson.TransferIn("2026-10-20", 700m, "CAR DEALER SA", "REMB AC VN SUITE REFUS PRET"),
			BankJson.TransferIn("2026-10-21", 5000m, "CREDIT BANK", "Payout", "CAJT"),
			BankJson.TransferIn("2026-09-29", 2500m, "ACME NV", PayrollRemittance));
		var month = analysis.MonthData("2026-10");

		analysis.Behind("2026-10", MonthFigure.Earnings).Sum(i => i.T.Amount).Should().Be(month.Income).And.Be(2560m);
		analysis.Behind("2026-10", MonthFigure.Refunds).Sum(i => i.T.Amount).Should().Be(month.Refunds).And.Be(700m);
		analysis.Behind("2026-10", MonthFigure.LoanReceived).Sum(i => i.T.Amount).Should().Be(month.LoanReceived).And.Be(5000m);
		analysis.Behind("2026-09", MonthFigure.Earnings).Should().ContainSingle();
	}
	#endregion
}
