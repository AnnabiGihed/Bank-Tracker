using System.Globalization;
using System.Text.Json.Nodes;

namespace KbcTracker.Domain.Tests.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds transactions shaped like the JSON KBC returns through Enable Banking, as the infrastructure
///              mapper turns them into <see cref="Txn"/>: debits negative, the other party as counterparty, and the
///              original JSON kept in <see cref="Txn.Raw"/> for the analysis. The mapper itself is tested in
///              KbcTracker.Infrastructure.Tests.
///
///              Every name, IBAN and reference used with it in the tests is invented; the account holder is
///              <see cref="Holder"/>, the debtor of every debit, as on a real statement.
/// </summary>
internal static class BankJson
{
	#region Constants
	/// <summary>
	/// The invented account holder, written as the debtor of every debit.
	/// </summary>
	public const string Holder = "JANE DOE";

	/// <summary>
	/// The invented Enable Banking account id.
	/// </summary>
	public const string AccountUid = "acc-1";
	#endregion

	#region Public Methods
	/// <summary>
	/// Creates a card payment the way KBC labels it ("&lt;shop&gt; &lt;country+postcode&gt; &lt;city&gt; Payment with …").
	/// </summary>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount paid, as a positive number.</param>
	/// <param name="shopLabel">The text before "Payment with", e.g. <c>WL*GOOGLE YouTube IED02 R296 Dublin</c>.</param>
	/// <returns>The mapped transaction.</returns>
	public static Txn Card(string date, decimal amount, string shopLabel) =>
		Debit(date, amount, "CCRD", "POSD", creditor: null, creditorIban: null,
			$"{shopLabel} Payment with Google Pay via Debit Mastercard {date} at 12.00 time 5127 88XX XXXX 0000 {Holder}");

	/// <summary>
	/// Creates an outgoing SEPA credit transfer.
	/// </summary>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount sent, as a positive number.</param>
	/// <param name="creditor">The receiver's name.</param>
	/// <param name="creditorIban">The receiver's IBAN.</param>
	/// <returns>The mapped transaction.</returns>
	public static Txn TransferOut(string date, decimal amount, string creditor, string creditorIban) =>
		Debit(date, amount, "ICDT", "ESCT", creditor, creditorIban, $"{creditor} Sending money {creditorIban}");

	/// <summary>
	/// Creates a loan instalment (ISO code <c>CSLN/PPAY</c>).
	/// </summary>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The instalment, as a positive number.</param>
	/// <param name="loanReference">The loan reference printed after "Reference".</param>
	/// <returns>The mapped transaction.</returns>
	public static Txn LoanInstalment(string date, decimal amount, string loanReference) =>
		Debit(date, amount, "CSLN", "PPAY", "KBC BANK NV", null, $"Loan repayment Reference {loanReference} capital and interest");

	/// <summary>
	/// Creates an incoming SEPA credit transfer.
	/// </summary>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount received, as a positive number.</param>
	/// <param name="debtor">The payer's name.</param>
	/// <param name="remittance">The remittance text.</param>
	/// <param name="subCode">The ISO sub code; <c>ESCT</c> for a transfer, <c>CAJT</c> for a loan payout.</param>
	/// <returns>The mapped transaction.</returns>
	public static Txn TransferIn(string date, decimal amount, string debtor, string remittance, string subCode = "ESCT") =>
		Map(date, amount, "CRDT", "RCDT", subCode, creditor: Holder, debtor: debtor, creditorIban: null, remittance);
	#endregion

	#region Private Helpers
	/// <summary>
	/// Creates a debit paid by <see cref="Holder"/>.
	/// </summary>
	private static Txn Debit(string date, decimal amount, string code, string subCode, string? creditor, string? creditorIban, string remittance) =>
		Map(date, amount, "DBIT", code, subCode, creditor, Holder, creditorIban, remittance);

	/// <summary>
	/// Builds the Enable Banking JSON with the fields KBC fills in, and the transaction the mapper makes of it.
	/// </summary>
	private static Txn Map(string date, decimal amount, string indicator, string code, string subCode,
		string? creditor, string? debtor, string? creditorIban, string remittance)
	{
		var json = new JsonObject
		{
			["entry_reference"] = $"{date}-{code}-{amount.ToString(CultureInfo.InvariantCulture)}-{creditor ?? remittance[..10]}",
			["transaction_amount"] = new JsonObject { ["currency"] = "EUR", ["amount"] = amount.ToString(CultureInfo.InvariantCulture) },
			["creditor"] = creditor is null ? null : new JsonObject { ["name"] = creditor },
			["creditor_account"] = creditorIban is null ? null : new JsonObject { ["iban"] = creditorIban },
			["debtor"] = debtor is null ? null : new JsonObject { ["name"] = debtor },
			["bank_transaction_code"] = new JsonObject { ["description"] = "PMNT", ["code"] = code, ["sub_code"] = subCode },
			["credit_debit_indicator"] = indicator,
			["status"] = "BOOK",
			["booking_date"] = date,
			["value_date"] = date,
			["remittance_information"] = remittance,
		};
		var debit = string.Equals(indicator, "DBIT", StringComparison.Ordinal);
		var counterparty = (debit ? creditor : debtor) ?? remittance[..Math.Min(remittance.Length, 45)];
		return new Txn(json["entry_reference"]!.GetValue<string>(), AccountUid, DateOnly.Parse(date, CultureInfo.InvariantCulture),
			debit ? -amount : amount, "EUR", counterparty, remittance, debit ? creditorIban?.Replace(" ", "", StringComparison.Ordinal) : null,
			"PMNT", json.ToJsonString());
	}
	#endregion
}
