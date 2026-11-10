using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Maps one transaction from the Enable Banking API JSON to a <see cref="Txn"/>.
///
///              Keeps the original JSON in <see cref="Txn.Raw"/> because the analysis reads fields from it
///              (ISO bank transaction code, creditor and debtor names) that are not mapped to their own properties.
/// </summary>
public static class EnableBankingMapper
{
	#region Public Methods
	/// <summary>
	/// Maps an Enable Banking transaction to a <see cref="Txn"/>. Debits get a negative amount.
	/// </summary>
	/// <param name="accountUid">The Enable Banking account the transaction belongs to.</param>
	/// <param name="transaction">The transaction JSON as returned by <c>GET /accounts/{uid}/transactions</c>.</param>
	/// <returns>The mapped transaction. Its id is the bank's entry reference or transaction id, or a hash of the
	/// account and JSON when the bank sends neither, so the same transaction always gets the same id.</returns>
	/// <exception cref="InvalidDataException">The JSON does not make a valid transaction (for example a malformed
	/// currency or no account).</exception>
	public static Txn Map(string accountUid, JsonNode transaction)
	{
		var amount = decimal.Parse(Text(transaction, "transaction_amount", "amount") ?? "0", CultureInfo.InvariantCulture);
		if (string.Equals(Text(transaction, "credit_debit_indicator"), "DBIT", StringComparison.Ordinal))
		{
			amount = -Math.Abs(amount);
		}

		var date = DateOnly.Parse(
			Text(transaction, "booking_date") ?? Text(transaction, "value_date") ?? Text(transaction, "transaction_date")
				?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			CultureInfo.InvariantCulture);

		var description = Description(transaction);
		var bankCode = Text(transaction, "bank_transaction_code", "description");
		var iban = amount < 0 ? Text(transaction, "creditor_account", "iban") : Text(transaction, "debtor_account", "iban");
		var counterparty = Present(amount < 0 ? Text(transaction, "creditor", "name") : Text(transaction, "debtor", "name"))
			?? Present(description.Length > 0 ? description[..Math.Min(description.Length, 45)] : null)
			?? Present(bankCode) ?? Present(iban) ?? "(unknown)";

		var id = IdOf(accountUid, transaction);
		var money = Required(Money.Create(amount, Text(transaction, "transaction_amount", "currency") ?? "EUR"));
		var party = Required(Counterparty.Create(counterparty, iban));
		return Required(Txn.Create(new TxnId(id), accountUid, date, money, party, description, new BankRecord(bankCode, transaction.ToJsonString())));
	}

	/// <summary>
	/// Gives the id a transaction gets: the bank's entry reference or transaction id, or a hash of the account and
	/// JSON when the bank sends neither. A blank reference counts as missing: as an id it would merge unrelated
	/// transactions. Also used to rebuild the id of a transaction an older version stored with a blank one, so a
	/// re-sync updates it instead of adding a copy.
	/// </summary>
	/// <param name="accountUid">The Enable Banking account the transaction belongs to.</param>
	/// <param name="transaction">The transaction JSON as returned by the bank.</param>
	/// <returns>The id; never blank.</returns>
	public static string IdOf(string accountUid, JsonNode transaction) =>
		Present(Text(transaction, "entry_reference")) ?? Present(Text(transaction, "transaction_id")) ?? ContentId(accountUid, transaction);
	#endregion

	#region Private Helpers
	/// <summary>
	/// Returns the text, or <see langword="null"/> when it is blank, so blank bank fields count as missing.
	/// </summary>
	private static string? Present(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

	/// <summary>
	/// Unwraps a domain result, turning a failure into an <see cref="InvalidDataException"/>: the bank sent data the
	/// domain rejects, which no retry can fix.
	/// </summary>
	private static T Required<T>(Result<T> result) =>
		result.IsSuccess ? result.Value : throw new InvalidDataException($"{result.Error.Code}: {result.Error.Message}");

	/// <summary>
	/// Derives an id from the account and the transaction JSON, for banks that send neither an entry reference nor
	/// a transaction id. The same transaction always gets the same id, so a re-sync does not duplicate it.
	/// </summary>
	private static string ContentId(string accountUid, JsonNode transaction)
	{
#pragma warning disable CA5350, S4790 // Not a security use: SHA-1 only fingerprints the content, and ids already stored in users' histories were made with it, so changing it would duplicate their transactions. Gihed Annabi 2026-11-09
		return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(accountUid + transaction.ToJsonString())));
#pragma warning restore CA5350, S4790
	}

	/// <summary>
	/// Reads a string value at a property path, or <see langword="null"/> when any step is missing or not a string.
	/// </summary>
	private static string? Text(JsonNode node, params string[] path)
	{
		JsonNode? current = node;
		foreach (var property in path)
		{
			current = current?[property];
		}

		return current is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
	}

	/// <summary>
	/// Joins the remittance information (a string or an array of lines) with the note, additional information and
	/// reference number into one description.
	/// </summary>
	private static string Description(JsonNode transaction)
	{
		var remittance = transaction["remittance_information"] is JsonArray lines
			? string.Join(" ", lines.Select(line => line?.ToString()).Where(line => !string.IsNullOrWhiteSpace(line)))
			: Text(transaction, "remittance_information") ?? "";
		var extra = string.Join(" ", new[]
		{
			Text(transaction, "note"), Text(transaction, "additional_information"), Text(transaction, "reference_number")
		}.Where(part => !string.IsNullOrWhiteSpace(part)));
		return string.Join(" ", new[] { remittance, extra }.Where(part => part.Length > 0)).Trim();
	}
	#endregion
}
