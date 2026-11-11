using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KbcTracker.Infrastructure.Features.BankConnection;

namespace KbcTracker.Infrastructure.Features.Legacy.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One transaction as stored in <c>transactions.json</c>. Keeps the file's shape unchanged while the
///              domain uses value objects, so existing histories load as they are.
/// </summary>
public sealed record TxnDocument
{
	#region Properties
	/// <summary>
	/// Gets the transaction id.
	/// </summary>
	public string Id { get; init; } = "";

	/// <summary>
	/// Gets the Enable Banking account id.
	/// </summary>
	public string AccountUid { get; init; } = "";

	/// <summary>
	/// Gets the booking date.
	/// </summary>
	public DateOnly Date { get; init; }

	/// <summary>
	/// Gets the signed amount.
	/// </summary>
	public decimal Amount { get; init; }

	/// <summary>
	/// Gets the currency code.
	/// </summary>
	public string Currency { get; init; } = "EUR";

	/// <summary>
	/// Gets the other party's name.
	/// </summary>
	public string Counterparty { get; init; } = "";

	/// <summary>
	/// Gets the description.
	/// </summary>
	public string Description { get; init; } = "";

	/// <summary>
	/// Gets the other party's IBAN.
	/// </summary>
	public string? CounterpartyIban { get; init; }

	/// <summary>
	/// Gets the ISO bank transaction code domain.
	/// </summary>
	public string? BankCode { get; init; }

	/// <summary>
	/// Gets the bank's original JSON.
	/// </summary>
	public string? Raw { get; init; }
	#endregion

	#region Public Methods
	/// <summary>
	/// Stores a transaction.
	/// </summary>
	/// <param name="txn">The transaction.</param>
	/// <returns>The document.</returns>
	public static TxnDocument From(Txn txn) => new()
	{
		Id = txn.Id.Value,
		AccountUid = txn.AccountUid,
		Date = txn.Date,
		Amount = txn.Amount,
		Currency = txn.Currency,
		Counterparty = txn.Counterparty,
		Description = txn.Description,
		CounterpartyIban = txn.CounterpartyIban,
		BankCode = txn.BankCode,
		Raw = txn.Raw,
	};

	/// <summary>
	/// Rebuilds the stored transaction as it is; missing text reads as empty. A blank id, which older versions could
	/// store, is rebuilt from the bank's JSON the way a sync builds it, so the next sync updates the transaction
	/// instead of adding a copy; without JSON it gets a stable id derived from its content.
	/// </summary>
	/// <returns>The transaction.</returns>
	public Txn ToDomain() => Txn.Rehydrate(
		new TxnId(string.IsNullOrWhiteSpace(Id) ? RebuiltId() : Id),
		AccountUid ?? "",
		Date,
		Money.Rehydrate(Amount, Currency ?? ""),
		Domain.Features.Transactions.ValueObjects.Counterparty.Rehydrate(Counterparty ?? "", CounterpartyIban),
		Description ?? "",
		new BankRecord(BankCode, Raw));
	#endregion

	#region Private Helpers
	/// <summary>
	/// Rebuilds a blank id from the bank's JSON, or derives one from the content when there is no readable JSON.
	/// </summary>
	private string RebuiltId()
	{
		var bankJson = ParseRaw();
		return bankJson is not null
			? EnableBankingMapper.IdOf(AccountUid ?? "", bankJson)
			: LegacyValues.IdOrDerived(Id, AccountUid, Date.ToString("O", CultureInfo.InvariantCulture), Amount.ToString(CultureInfo.InvariantCulture), Description);
	}

	/// <summary>
	/// Parses the stored bank JSON, or returns <see langword="null"/> when it is missing, not valid JSON, or not a
	/// JSON object (the only shape a bank transaction has).
	/// </summary>
	private JsonObject? ParseRaw()
	{
		if (string.IsNullOrWhiteSpace(Raw))
		{
			return null;
		}

		try
		{
			return JsonNode.Parse(Raw) as JsonObject;
		}
		catch (JsonException)
		{
			// Unreadable JSON only means the id cannot be rebuilt from it; the content-derived id is used instead.
			return null;
		}
	}
	#endregion
}
