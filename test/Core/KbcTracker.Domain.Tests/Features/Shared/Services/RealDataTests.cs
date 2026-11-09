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
///              skipped instead of passing (<see cref="RealDataFactAttribute"/>). It only reads the files.
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
		var txns = JsonSerializer.Deserialize<List<Txn>>(File.ReadAllText(RealDataFactAttribute.TransactionsFile), Json)!;
		var categories = JsonSerializer.Deserialize<List<Category>>(File.ReadAllText(RealDataFactAttribute.CategoriesFile), Json)!;

		var suggestions = new Analysis(txns, categories, new UserData()).Suggestions;

		var found = string.Join(", ", suggestions.Select(s => $"{s.Name} {s.Amount}"));
		suggestions.Any(s => s.Amount == 13.99m && s.Name.Contains("YouTube", StringComparison.OrdinalIgnoreCase)).Should().BeTrue(found);
		suggestions.Any(s => s.Amount == 940m).Should().BeTrue(found);
		suggestions.Any(s => s.Amount == 375.50m && s.Kind == Kind.Loan).Should().BeTrue(found);
	}
	#endregion
}
