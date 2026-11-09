using System.Text.Json.Nodes;

namespace KbcTracker.Domain.Features.Transactions.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The few fields the analysis reads from the bank's raw JSON (<see cref="Txn.Raw"/>): the ISO bank
///              transaction code and the creditor and debtor names and IBANs.
/// </summary>
/// <param name="Code">The ISO bank transaction code family, for example <c>CCRD</c>.</param>
/// <param name="SubCode">The ISO sub family, for example <c>POSD</c>.</param>
/// <param name="CreditorName">The receiver's name.</param>
/// <param name="DebtorName">The payer's name.</param>
/// <param name="CreditorIban">The receiver's IBAN without spaces.</param>
/// <param name="DebtorIban">The payer's IBAN without spaces.</param>
internal sealed record BankFields(string? Code, string? SubCode, string? CreditorName, string? DebtorName, string? CreditorIban, string? DebtorIban)
{
	#region Factory Methods
	/// <summary>
	/// Reads the fields from a transaction's raw JSON. Without raw JSON, the counterparty stands in for the creditor
	/// (money out) or debtor (money in).
	/// </summary>
	/// <param name="txn">The transaction.</param>
	/// <returns>The fields; blank values are <see langword="null"/> and other values are trimmed.</returns>
	public static BankFields From(Txn txn)
	{
		if (string.IsNullOrEmpty(txn.Raw))
		{
			return new(null, null, txn.Amount < 0 ? txn.Counterparty : null, txn.Amount > 0 ? txn.Counterparty : null, txn.CounterpartyIban, null);
		}

		var json = JsonNode.Parse(txn.Raw)!;
		return new(
			Text(json["bank_transaction_code"]?["code"]), Text(json["bank_transaction_code"]?["sub_code"]),
			Text(json["creditor"]?["name"]), Text(json["debtor"]?["name"]),
			Text(json["creditor_account"]?["iban"])?.Replace(" ", "", StringComparison.Ordinal),
			Text(json["debtor_account"]?["iban"])?.Replace(" ", "", StringComparison.Ordinal));
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Reads a non-blank string value, trimmed, or <see langword="null"/>.
	/// </summary>
	private static string? Text(JsonNode? node) =>
		node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
	#endregion
}
