namespace KbcTracker.Domain.Tests.Features.Debts.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins how a debt is created, revised and corrected by hand (ADR-0010), and how <see cref="UserData"/>
///              adds, changes and removes debts.
/// </summary>
public sealed class DebtTests
{
	#region Test Infrastructure
	/// <summary>
	/// Terms of a valid loan from a friend; each test changes what it checks.
	/// </summary>
	private static DebtTerms Terms(string lender = "Friend", string? iban = null, decimal principal = 1000m, string? seriesKey = null, TxnId? received = null, DateOnly? borrowedOn = null) =>
		new(DebtKind.PersonalLoan, lender, iban, received, principal, seriesKey, null, borrowedOn ?? new DateOnly(2026, 10, 1));

	/// <summary>
	/// Creates a debt the test expects to be valid.
	/// </summary>
	private static Debt Valid(string id = "d1", DebtTerms? terms = null) => Debt.Create(new DebtId(id), terms ?? Terms()).Value;
	#endregion

	#region Create Tests
	/// <summary>
	/// Verifies that the lender is trimmed and the IBAN loses its spaces and becomes upper case.
	/// </summary>
	[Fact]
	public void Create_Valid_NormalisesLenderAndIban()
	{
		var debt = Valid(terms: Terms("  Friend One ", "be00 1111 2222 3333"));

		debt.Lender.Should().Be("Friend One");
		debt.Iban.Should().Be("BE00111122223333");
		debt.AmountToRepay.Should().Be(1000m, "Without a plan the amount received is what is to repay.");
	}

	/// <summary>
	/// Verifies that a blank or too long lender is refused.
	/// </summary>
	[Fact]
	public void Create_BadLender_Fails()
	{
		Debt.Create(new DebtId("d1"), Terms("  ")).Error.Should().Be(DebtErrors.LenderInvalid);
		Debt.Create(new DebtId("d1"), Terms(new string('x', 101))).Error.Should().Be(DebtErrors.LenderInvalid);
		Debt.Create(new DebtId("d1"), Terms(new string('x', 100))).IsSuccess.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that an amount received that is not positive or has more than two decimals is refused.
	/// </summary>
	/// <param name="principal">The amount received.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(10.001)]
	public void Create_BadPrincipal_Fails(double principal)
	{
		Debt.Create(new DebtId("d1"), Terms(principal: (decimal)principal)).Error.Should().Be(DebtErrors.PrincipalInvalid);
	}

	/// <summary>
	/// Verifies that an IBAN with a symbol or more than 34 characters is refused.
	/// </summary>
	[Fact]
	public void Create_BadIban_Fails()
	{
		Debt.Create(new DebtId("d1"), Terms(iban: "BE00-1111")).Error.Should().Be(DebtErrors.IbanInvalid);
		Debt.Create(new DebtId("d1"), Terms(iban: new string('1', 35))).Error.Should().Be(DebtErrors.IbanInvalid);
	}

	/// <summary>
	/// Verifies that the borrowed-on day must be a plausible year and is kept.
	/// </summary>
	[Fact]
	public void Create_BorrowedOn_MustBePlausibleAndIsKept()
	{
		Debt.Create(new DebtId("d1"), Terms(borrowedOn: new DateOnly(1999, 12, 31))).Error.Should().Be(DebtErrors.BorrowedOnInvalid);
		Debt.Create(new DebtId("d1"), Terms(borrowedOn: new DateOnly(2026, 9, 3))).Value.BorrowedOn.Should().Be(new DateOnly(2026, 9, 3));
	}

	/// <summary>
	/// Verifies that a series key must start with <c>LOAN:</c>.
	/// </summary>
	[Fact]
	public void Create_SeriesKey_MustStartWithLoan()
	{
		Debt.Create(new DebtId("d1"), Terms(seriesKey: "CARD:SHOP")).Error.Should().Be(DebtErrors.SeriesKeyInvalid);
		Debt.Create(new DebtId("d1"), Terms(seriesKey: "LOAN:726-1")).Value.SeriesKey.Should().Be("LOAN:726-1");
	}
	#endregion

	#region Correction Tests
	/// <summary>
	/// Verifies that adding, excluding and resetting a payment move it between the lists, a payment being in one list at
	/// most.
	/// </summary>
	[Fact]
	public void IncludeExcludeReset_MoveThePaymentBetweenLists()
	{
		var debt = Valid();
		var payment = new TxnId("t1");

		debt.Include(payment);
		debt.IncludedPayments.Should().Contain(payment);

		debt.Exclude(payment);
		debt.IncludedPayments.Should().BeEmpty();
		debt.ExcludedPayments.Should().Contain(payment);

		debt.Include(payment);
		debt.ExcludedPayments.Should().BeEmpty();

		debt.Reset(payment);
		debt.IncludedPayments.Should().BeEmpty();
		debt.ExcludedPayments.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that revising keeps the corrections made by hand, and that a refused revision changes nothing.
	/// </summary>
	[Fact]
	public void Revise_KeepsCorrections_AndFailureChangesNothing()
	{
		var debt = Valid();
		debt.Include(new TxnId("t1"));

		debt.Revise(Terms("Other", principal: 500m)).IsSuccess.Should().BeTrue();
		debt.Lender.Should().Be("Other");
		debt.Principal.Should().Be(500m);
		debt.IncludedPayments.Should().Contain(new TxnId("t1"));

		debt.Revise(Terms("", principal: 900m)).IsFailure.Should().BeTrue();
		debt.Lender.Should().Be("Other");
		debt.Principal.Should().Be(500m);
	}
	#endregion

	#region UserData Tests
	/// <summary>
	/// Verifies that the user's decisions add, update and remove a debt, and report a missing one as not found.
	/// </summary>
	[Fact]
	public void UserData_AddUpdateRemove_Works()
	{
		var user = UserData.Empty();
		var id = new DebtId("d1");

		user.AddDebt(id, Terms()).IsSuccess.Should().BeTrue();
		user.Debts.Should().ContainSingle();

		user.UpdateDebt(id, Terms("Changed")).IsSuccess.Should().BeTrue();
		user.Debts.Single().Lender.Should().Be("Changed");

		user.RemoveDebt(id).IsSuccess.Should().BeTrue();
		user.Debts.Should().BeEmpty();
		user.RemoveDebt(id).Error.Should().Be(DebtErrors.NotFound);
		user.UpdateDebt(id, Terms()).Error.Should().Be(DebtErrors.NotFound);
	}

	/// <summary>
	/// Verifies that an invalid debt is not added.
	/// </summary>
	[Fact]
	public void UserData_AddInvalidDebt_NotAdded()
	{
		var user = UserData.Empty();

		user.AddDebt(new DebtId("d1"), Terms("")).IsFailure.Should().BeTrue();

		user.Debts.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that two debts cannot track the same received payment, on adding or updating.
	/// </summary>
	[Fact]
	public void UserData_SameReceivedPayment_Refused()
	{
		var user = UserData.Empty();
		var payout = new TxnId("payout");
		user.AddDebt(new DebtId("d1"), Terms(received: payout)).IsSuccess.Should().BeTrue();

		user.AddDebt(new DebtId("d2"), Terms(received: payout)).Error.Should().Be(DebtErrors.ReceivedPaymentTaken);
		user.AddDebt(new DebtId("d3"), Terms()).IsSuccess.Should().BeTrue();
		user.UpdateDebt(new DebtId("d3"), Terms(received: payout)).Error.Should().Be(DebtErrors.ReceivedPaymentTaken);
		user.UpdateDebt(new DebtId("d1"), Terms("Same debt again", received: payout)).IsSuccess.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that a payment cannot be included in two debts, and that excluding and resetting work per debt.
	/// </summary>
	[Fact]
	public void UserData_IncludeRepayment_OnlyOneDebtPerPayment()
	{
		var user = UserData.Empty();
		user.AddDebt(new DebtId("d1"), Terms()).IsSuccess.Should().BeTrue();
		user.AddDebt(new DebtId("d2"), Terms("Other")).IsSuccess.Should().BeTrue();
		var payment = new TxnId("t1");

		user.IncludeRepayment(new DebtId("d1"), payment).IsSuccess.Should().BeTrue();
		user.IncludeRepayment(new DebtId("d2"), payment).Error.Should().Be(DebtErrors.PaymentTaken);
		user.ExcludeRepayment(new DebtId("d2"), payment).IsSuccess.Should().BeTrue();
		user.Debts.Single(d => d.Id == new DebtId("d2")).ExcludedPayments.Should().Contain(payment);

		user.ResetRepayment(new DebtId("d2"), payment).IsSuccess.Should().BeTrue();
		user.Debts.Single(d => d.Id == new DebtId("d2")).ExcludedPayments.Should().BeEmpty();
		user.IncludeRepayment(new DebtId("missing"), payment).Error.Should().Be(DebtErrors.NotFound);
		user.ExcludeRepayment(new DebtId("missing"), payment).Error.Should().Be(DebtErrors.NotFound);
		user.ResetRepayment(new DebtId("missing"), payment).Error.Should().Be(DebtErrors.NotFound);
	}
	#endregion
}
