namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins the totals rule of ADR-0010: bank loan instalments and payments matched to a debt are never
///              spending, in the month summary and in the lists behind its figures, while the fixed-payment suggestion
///              of a loan instalment stays as it was.
/// </summary>
public sealed class AnalysisRepaymentTests
{
	#region Test Infrastructure
	/// <summary>
	/// The fixed "today" of every analysis in this class.
	/// </summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>
	/// Runs the analysis over the payments with the given decisions.
	/// </summary>
	private static Analysis Analyse(IEnumerable<Txn> txns, UserData? user = null) =>
		new([.. txns], CategoryDefaults.CreateDefaults("BE"), user ?? UserData.Empty(), Today);

	/// <summary>
	/// Decisions with one loan from a friend, repaid by transfers to <c>BE00111122223333</c>.
	/// </summary>
	private static UserData WithFriendDebt()
	{
		var user = UserData.Empty();
		var terms = new DebtTerms(DebtKind.PersonalLoan, "Friend One", "BE00111122223333", null, 1000m, null, null, new DateOnly(2026, 1, 1));
		user.AddDebt(new DebtId("friend"), terms).IsSuccess.Should().BeTrue();
		return user;
	}
	#endregion

	#region Totals Tests
	/// <summary>
	/// Verifies that a loan instalment is no spending even when no debt is tracked, and no fixed payment either.
	/// </summary>
	[Fact]
	public void MonthSummary_LoanInstalment_NotSpending()
	{
		var analysis = Analyse(
		[
			BankJson.LoanInstalment("2026-11-03", 375.50m, "726-0000000-01"),
			BankJson.Card("2026-11-04", 12m, "SHOP ONE BE1000 Bruxelles"),
		]);

		var month = analysis.MonthData("2026-11");
		month.Spent.Should().Be(12m);
		month.ByCategory.Should().NotContainKey("loan");
		analysis.Items.Single(i => i.Kind == Kind.Loan).Repayment.Should().BeTrue();
		analysis.Items.Single(i => i.Kind == Kind.Loan).IsSpending.Should().BeFalse();
	}

	/// <summary>
	/// Verifies that payments matched to a debt are no spending, and that a payment to the same friend after the debt is
	/// covered is.
	/// </summary>
	[Fact]
	public void MonthSummary_MatchedRepayment_NotSpending()
	{
		var user = WithFriendDebt();
		var analysis = Analyse(
		[
			BankJson.TransferOut("2026-11-03", 400m, "Friend One", "BE00111122223333"),
			BankJson.TransferOut("2026-11-04", 50m, "Someone Else", "BE00999988887777"),
		], user);

		var month = analysis.MonthData("2026-11");
		month.Spent.Should().Be(50m, "Only the payment to someone else is spending.");
		month.Saved.Should().Be(0m);
		analysis.Debts.Single().Paid.Should().Be(400m);
		analysis.Items.Single(i => i.T.Amount == -400m).IsSpending.Should().BeFalse();
		analysis.Items.Single(i => i.T.Amount == -50m).IsSpending.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that without a debt the same transfer to the friend is spending, so the rule applies to matched payments
	/// only.
	/// </summary>
	[Fact]
	public void MonthSummary_NoDebt_TransferStillSpending()
	{
		var analysis = Analyse([BankJson.TransferOut("2026-11-03", 400m, "Friend One", "BE00111122223333")]);

		analysis.MonthData("2026-11").Spent.Should().Be(400m);
	}

	/// <summary>
	/// Verifies that a repayment accepted as a fixed payment is left out of the fixed total as well.
	/// </summary>
	[Fact]
	public void MonthSummary_AcceptedLoanFixedPayment_NotInFixedTotal()
	{
		var first = Analyse(
		[
			BankJson.LoanInstalment("2026-09-07", 375.50m, "726-0000000-01"),
			BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01"),
			BankJson.LoanInstalment("2026-11-09", 375.50m, "726-0000000-01"),
		]);
		var suggestion = first.Suggestions.Single(s => s.Kind == Kind.Loan);
		var user = UserData.Empty();
		user.AcceptFixedPayment(Analysis.ToDefinition(suggestion, new RecurringPaymentId("loan"), DateTimeOffset.UnixEpoch).Value);

		var analysis = Analyse(first.Items.Select(i => i.T), user);

		analysis.MonthData("2026-11").Fixed.Should().Be(0m);
		analysis.Fixed.Single().Payments.Should().HaveCount(3, "Accepting the suggestion still works; only the totals change.");
	}

	/// <summary>
	/// Verifies that the loan instalment stays a fixed-payment suggestion: the totals rule does not touch suggestions.
	/// </summary>
	[Fact]
	public void Suggestions_LoanInstalment_StillSuggested()
	{
		var analysis = Analyse(
		[
			BankJson.LoanInstalment("2026-09-07", 375.50m, "726-0000000-01"),
			BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01"),
			BankJson.LoanInstalment("2026-11-09", 375.50m, "726-0000000-01"),
		]);

		analysis.Suggestions.Should().Contain(s => s.Amount == 375.50m && s.Kind == Kind.Loan);
	}

	/// <summary>
	/// Verifies that the lists behind the money-in figures still add up to them when repayments are present.
	/// </summary>
	[Fact]
	public void Behind_WithRepayments_StillSumsToTheFigures()
	{
		var user = WithFriendDebt();
		var borrowed = BankJson.TransferIn("2026-11-02", 300m, "Friend One", "Thanks", debtorIban: "BE00111122223333");
		user.AssignTransactionCategory(borrowed.Id, CategoryId.BorrowedPeople);
		var analysis = Analyse([borrowed, BankJson.TransferOut("2026-11-03", 100m, "Friend One", "BE00111122223333"), BankJson.TransferIn("2026-11-05", 2000m, "Employer", "Salary")], user);

		var month = analysis.MonthData("2026-11");
		analysis.Behind("2026-11", MonthFigure.LoanReceived).Sum(i => i.T.Amount).Should().Be(month.LoanReceived).And.Be(300m);
		analysis.Behind("2026-11", MonthFigure.Earnings).Sum(i => i.T.Amount).Should().Be(month.Income).And.Be(2000m);
	}
	#endregion

	#region Owner Decision Tests
	/// <summary>
	/// Verifies that a payment the owner split keeps its spending even when it is matched to a debt.
	/// </summary>
	[Fact]
	public void MonthSummary_SplitMatchedPayment_StillSpending()
	{
		var user = WithFriendDebt();
		var payment = BankJson.TransferOut("2026-11-03", 400m, "Friend One", "BE00111122223333");
		user.SplitPayment(payment.Id, PaymentSplit.Create(-400m, [new SplitLine(150m, new CategoryId("other"), "Dinner"), new SplitLine(250m, new CategoryId("bills"), null)]).Value);

		var analysis = Analyse([payment], user);

		analysis.Items.Single().Repayment.Should().BeFalse();
		analysis.MonthData("2026-11").Spent.Should().Be(400m);
		analysis.Debts.Single().Paid.Should().Be(400m, "The debt still counts the payment; only the totals keep it as spending.");
	}

	/// <summary>
	/// Verifies that a category the owner chose for a matched payment, or for its payee, keeps it as spending.
	/// </summary>
	/// <param name="onPayee">Whether the category is set on the payee instead of the payment.</param>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void MonthSummary_OwnerCategoryOnMatchedPayment_StillSpending(bool onPayee)
	{
		var user = WithFriendDebt();
		var payment = BankJson.TransferOut("2026-11-03", 400m, "Friend One", "BE00111122223333");
		var key = Analyse([payment]).Items.Single().SeriesKey;
		if (onPayee)
		{
			user.AssignPayeeCategory(key, new CategoryId("other"));
		}
		else
		{
			user.AssignTransactionCategory(payment.Id, new CategoryId("other"));
		}

		var analysis = Analyse([payment], user);

		analysis.Items.Single().Repayment.Should().BeFalse();
		analysis.MonthData("2026-11").Spent.Should().Be(400m);
	}

	/// <summary>
	/// Verifies that a bank loan instalment the owner categorised is spending, like any other payment the owner decided.
	/// </summary>
	[Fact]
	public void MonthSummary_LoanInstalmentWithOwnerCategory_StillSpending()
	{
		var user = UserData.Empty();
		var instalment = BankJson.LoanInstalment("2026-11-03", 375.50m, "726-0000000-01");
		user.AssignTransactionCategory(instalment.Id, new CategoryId("other"));

		var analysis = Analyse([instalment], user);

		analysis.Items.Single().Repayment.Should().BeFalse();
		analysis.MonthData("2026-11").Spent.Should().Be(375.50m);
	}
	#endregion
}
