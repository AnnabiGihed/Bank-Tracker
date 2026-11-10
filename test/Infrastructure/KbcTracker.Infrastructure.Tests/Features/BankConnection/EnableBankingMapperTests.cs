using System.Text.Json.Nodes;

namespace KbcTracker.Infrastructure.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that <see cref="EnableBankingMapper"/> turns Enable Banking transaction JSON into
///              <see cref="Txn"/> values: sign of the amount, counterparty, description and a stable id.
/// </summary>
public sealed class EnableBankingMapperTests
{
	#region Mapping Tests
	/// <summary>
	/// Verifies that a debit gets a negative amount and the creditor as counterparty, with the IBAN compacted.
	/// </summary>
	[Fact]
	public void Map_Debit_NegativeAmountAndCreditorAsCounterparty()
	{
		var txn = BankJson.TransferOut("2026-10-01", 940m, "LANDLORD ONE", "BE00 1111 2222 3333");

		txn.Amount.Should().Be(-940m, "A DBIT must be stored as money out.");
		txn.Counterparty.Should().Be("LANDLORD ONE");
		txn.CounterpartyIban.Should().Be("BE00111122223333", "IBAN spaces are removed.");
		txn.Date.Should().Be(new DateOnly(2026, 10, 1));
		txn.Raw.Should().NotBeNull("The raw JSON is kept for the analysis.");
	}

	/// <summary>
	/// Verifies that a credit keeps a positive amount and uses the debtor as counterparty.
	/// </summary>
	[Fact]
	public void Map_Credit_PositiveAmountAndDebtorAsCounterparty()
	{
		var txn = BankJson.TransferIn("2026-10-30", 2500m, "ACME PAYROLL", "Salary October");

		txn.Amount.Should().Be(2500m);
		txn.Counterparty.Should().Be("ACME PAYROLL");
	}

	/// <summary>
	/// Verifies that a transaction without entry reference or transaction id gets an id derived from its
	/// content, so syncing it twice does not create a duplicate.
	/// </summary>
	[Fact]
	public void Map_NoBankId_SameJsonGivesSameId()
	{
		var json = """{"transaction_amount":{"currency":"EUR","amount":"5.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-02","remittance_information":"Coffee"}""";

		var first = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);
		var second = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		second.Id.Should().Be(first.Id);
		first.Id.Value.Length.Should().Be(40, "The fallback id is a SHA-1 hex string.");
	}

	/// <summary>
	/// Verifies that remittance information sent as an array of lines is joined into one description, and that
	/// the description stands in for a missing counterparty name.
	/// </summary>
	[Fact]
	public void Map_RemittanceArray_JoinedIntoDescription()
	{
		var json = """{"entry_reference":"x1","transaction_amount":{"currency":"EUR","amount":"12.50"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-03","remittance_information":["Line one"," ","Line two"]}""";

		var txn = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		txn.Description.Should().Be("Line one Line two");
		txn.Counterparty.Should().Be("Line one Line two");
	}

	/// <summary>
	/// Verifies that a blank entry reference counts as missing: the id falls back to the content hash instead of
	/// being blank, which would merge unrelated transactions.
	/// </summary>
	[Fact]
	public void Map_BlankEntryReference_ContentId()
	{
		var json = """{"entry_reference":"","transaction_id":" ","transaction_amount":{"currency":"EUR","amount":"5.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-02","remittance_information":"Coffee"}""";

		var txn = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		txn.Id.Value.Should().HaveLength(40, "The fallback id is a SHA-1 hex string.");
	}

	/// <summary>
	/// Verifies that a blank entry reference falls back to the bank's transaction id when there is one.
	/// </summary>
	[Fact]
	public void Map_BlankEntryReferenceWithTransactionId_TransactionId()
	{
		var json = """{"entry_reference":" ","transaction_id":"tid-7","transaction_amount":{"currency":"EUR","amount":"5.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-02","remittance_information":"Coffee"}""";

		var txn = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		txn.Id.Value.Should().Be("tid-7");
	}

	/// <summary>
	/// Verifies that a blank creditor name and IBAN count as missing: the description names the payee and no IBAN
	/// is kept.
	/// </summary>
	[Fact]
	public void Map_BlankCreditorNameAndIban_DescriptionAndNoIban()
	{
		var json = """{"entry_reference":"x4","transaction_amount":{"currency":"EUR","amount":"5.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-02","creditor":{"name":"  "},"creditor_account":{"iban":" "},"remittance_information":"Bakery"}""";

		var txn = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		txn.Counterparty.Should().Be("Bakery");
		txn.CounterpartyIban.Should().BeNull();
	}

	/// <summary>
	/// Verifies that a transaction without any name, description, bank code or IBAN still gets a counterparty.
	/// </summary>
	[Fact]
	public void Map_NothingToNameIt_UnknownCounterparty()
	{
		var json = """{"entry_reference":"x3","transaction_amount":{"currency":"EUR","amount":"5.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-02","bank_transaction_code":{"description":""}}""";

		var txn = EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		txn.Counterparty.Should().Be("(unknown)");
	}

	/// <summary>
	/// Verifies that JSON the domain rejects (here a malformed currency) is reported as invalid data, which the
	/// transaction source skips, instead of being stored.
	/// </summary>
	[Fact]
	public void Map_InvalidCurrency_ThrowsInvalidDataException()
	{
		var json = """{"entry_reference":"x2","transaction_amount":{"currency":"euro","amount":"1.00"},"credit_debit_indicator":"DBIT","booking_date":"2026-10-03","remittance_information":"Coffee"}""";

		var map = () => EnableBankingMapper.Map("acc-1", JsonNode.Parse(json)!);

		map.Should().Throw<InvalidDataException>().WithMessage("*Txn.Currency.Invalid*");
	}
	#endregion
}
