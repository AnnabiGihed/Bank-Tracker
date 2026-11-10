using System.Text.Json;
using System.Text.Json.Serialization;

namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Checks the fixed-payment detection against the user's real history in
///              <c>%LOCALAPPDATA%\KbcTracker</c>, as the project instructions ask for any detection change.
///
///              The data is personal and never committed, so on a machine without it the test is reported as
///              skipped instead of passing (<see cref="RealDataFactAttribute"/>). It only reads the files, through
///              records of their own shape, since the domain types are not serialised directly.
/// </summary>
[Trait("Category", "RealData")]
public sealed class RealDataTests
{
	#region Test Infrastructure
	/// <summary>
	/// Serializer settings matching the web host's JSON store (enums as strings).
	/// </summary>
	private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Suggestion Tests
	/// <summary>
	/// Verifies that, starting from no accepted fixed payments, the real history still yields the suggestions the
	/// user accepted: Google YouTube 13.99 €, rent 940 € and the loan instalment 375.50 €.
	/// </summary>
	[RealDataFact]
	public void Suggestions_RealHistory_KnownFixedPaymentsSuggested()
	{
		var txns = JsonSerializer.Deserialize<List<StoredTxn>>(File.ReadAllText(RealDataFactAttribute.TransactionsFile), Json)!
			.Select(t => t.ToDomain());
		var categories = JsonSerializer.Deserialize<List<StoredCategory>>(File.ReadAllText(RealDataFactAttribute.CategoriesFile), Json)!
			.Select(c => c.ToDomain());

		var suggestions = new Analysis(txns, categories, UserData.Empty()).Suggestions;

		var found = string.Join(", ", suggestions.Select(s => $"{s.Name} {s.Amount}"));
		suggestions.Any(s => s.Amount == 13.99m && s.Name.Contains("YouTube", StringComparison.OrdinalIgnoreCase)).Should().BeTrue(found);
		suggestions.Any(s => s.Amount == 940m).Should().BeTrue(found);
		suggestions.Any(s => s.Amount == 375.50m && s.Kind == Kind.Loan).Should().BeTrue(found);
	}
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : A transaction as stored in <c>transactions.json</c>.
	/// </summary>
	/// <param name="Id">The transaction id.</param>
	/// <param name="AccountUid">The account id.</param>
	/// <param name="Date">The booking date.</param>
	/// <param name="Amount">The signed amount.</param>
	/// <param name="Currency">The currency code.</param>
	/// <param name="Counterparty">The other party's name.</param>
	/// <param name="Description">The description.</param>
	/// <param name="CounterpartyIban">The other party's IBAN.</param>
	/// <param name="BankCode">The ISO bank transaction code domain.</param>
	/// <param name="Raw">The bank's original JSON.</param>
	private sealed record StoredTxn(string Id, string AccountUid, DateOnly Date, decimal Amount, string Currency, string Counterparty,
		string Description, string? CounterpartyIban, string? BankCode, string? Raw)
	{
		#region Public Methods
		/// <summary>
		/// Rebuilds the domain transaction.
		/// </summary>
		/// <returns>The transaction.</returns>
		public Txn ToDomain() => Txn.Rehydrate(new TxnId(Id), AccountUid, Date, Money.Rehydrate(Amount, Currency),
			Domain.Features.Transactions.ValueObjects.Counterparty.Rehydrate(Counterparty, CounterpartyIban), Description, new BankRecord(BankCode, Raw));
		#endregion
	}

	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : A category as stored in <c>categories.json</c>.
	/// </summary>
	/// <param name="Id">The category id.</param>
	/// <param name="Name">The name.</param>
	/// <param name="Type">How its payments count.</param>
	/// <param name="Color">The colour.</param>
	/// <param name="Keywords">The keywords.</param>
	/// <param name="BuiltIn">Whether the analysis relies on it.</param>
	private sealed record StoredCategory(string Id, string Name, CategoryType Type, string Color, IReadOnlyList<string> Keywords, bool BuiltIn)
	{
		#region Public Methods
		/// <summary>
		/// Rebuilds the domain category.
		/// </summary>
		/// <returns>The category.</returns>
		public Category ToDomain() => Category.Rehydrate(new CategoryId(Id), Name, Type, Color, Keywords, BuiltIn);
		#endregion
	}
	#endregion
}
