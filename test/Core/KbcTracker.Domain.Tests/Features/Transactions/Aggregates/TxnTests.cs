namespace KbcTracker.Domain.Tests.Features.Transactions.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the creation rules of <see cref="Txn"/> and its value objects <see cref="Money"/> and
///              <see cref="Counterparty"/>, and the ordinal equality of the string identifiers.
/// </summary>
public sealed class TxnTests
{
	#region Creation Tests
	/// <summary>
	/// Verifies that a valid transaction is created with every value carried over.
	/// </summary>
	[Fact]
	public void Create_ValidValues_Created()
	{
		var result = Txn.Create(new TxnId("t1"), "acc-1", new DateOnly(2026, 11, 2), Money.Create(-10m, "EUR").Value,
			Counterparty.Create(" Shop ", "BE00 1111 2222 3333").Value, "Coffee", new BankRecord("PMNT", "{}"));

		result.IsSuccess.Should().BeTrue();
		result.Value.Amount.Should().Be(-10m);
		result.Value.Counterparty.Should().Be("Shop", "The name is trimmed.");
		result.Value.CounterpartyIban.Should().Be("BE00111122223333", "IBAN spaces are removed.");
		result.Value.BankCode.Should().Be("PMNT");
	}

	/// <summary>
	/// Verifies that a transaction without account is refused.
	/// </summary>
	[Fact]
	public void Create_BlankAccount_AccountRequired()
	{
		var result = Txn.Create(new TxnId("t1"), " ", new DateOnly(2026, 11, 2), Money.Rehydrate(-10m, "EUR"),
			Counterparty.Rehydrate("Shop", null), "", BankRecord.None);

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(TxnErrors.AccountRequired);
	}

	/// <summary>
	/// Verifies that only three upper-case letters make a currency.
	/// </summary>
	/// <param name="currency">The currency code.</param>
	/// <param name="valid">Whether it is accepted.</param>
	[Theory]
	[InlineData("EUR", true)]
	[InlineData("eur", false)]
	[InlineData("EURO", false)]
	[InlineData("", false)]
	public void MoneyCreate_Currency_OnlyIsoCodes(string currency, bool valid)
	{
		var result = Money.Create(1m, currency);

		result.IsSuccess.Should().Be(valid);
	}

	/// <summary>
	/// Verifies that a counterparty needs a name.
	/// </summary>
	[Fact]
	public void CounterpartyCreate_BlankName_Required()
	{
		var result = Counterparty.Create("  ", "BE00111122223333");

		result.Error.Should().Be(TxnErrors.CounterpartyRequired);
	}
	#endregion

	#region Identifier Tests
	/// <summary>
	/// Verifies that string identifiers compare by value, ordinally and case-sensitively.
	/// </summary>
	[Fact]
	public void TxnId_SameValue_EqualAndOrdinal()
	{
		new TxnId("abc").Should().Be(new TxnId("abc"));
		new TxnId("abc").Should().NotBe(new TxnId("ABC"));
		(new TxnId("B") < new TxnId("a")).Should().BeTrue("Upper case sorts first ordinally.");
	}

	/// <summary>
	/// Verifies the ordering operators of the string identifiers, ordinal and with <see langword="null"/> first.
	/// </summary>
	[Fact]
	public void TxnId_Operators_OrdinalOrder()
	{
		var a = new TxnId("a");
		var b = new TxnId("b");

		(a < b).Should().BeTrue();
		(a <= new TxnId("a")).Should().BeTrue();
		(b > a).Should().BeTrue();
		(b >= new TxnId("b")).Should().BeTrue();
		(null < a).Should().BeTrue("A missing id sorts first.");
		a.CompareTo(null).Should().BePositive();
	}

	/// <summary>
	/// Verifies that an identifier cannot be blank.
	/// </summary>
	[Fact]
	public void TxnId_Blank_Throws()
	{
		var create = () => new TxnId(" ");

		create.Should().Throw<ArgumentException>();
	}
	#endregion
}
