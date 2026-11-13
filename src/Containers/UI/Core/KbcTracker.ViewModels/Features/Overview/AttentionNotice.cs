namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The banner that points to the fixed payment and subscription suggestions to check and the payees to review.
/// </summary>
/// <param name="Suggestions">The suggestions that wait, fixed payments and subscriptions.</param>
/// <param name="ReviewCount">How many payees have an automatic category to confirm.</param>
public sealed record AttentionNotice(WaitingSuggestions Suggestions, int ReviewCount)
{
	#region Properties
	/// <summary>Gets the words about the suggestions, or <see langword="null"/> when there are none.</summary>
	public string? SuggestionText => Suggestions.Text;

	/// <summary>Gets the words about the payees to review, or <see langword="null"/> when there are none.</summary>
	public string? ReviewText => ReviewCount > 0 ? $"{ReviewCount} payee(s) with an automatic category to confirm." : null;
	#endregion
}
