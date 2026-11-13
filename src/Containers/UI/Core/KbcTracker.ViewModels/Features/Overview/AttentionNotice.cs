namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The banner that points to the fixed payment suggestions to check and the payees to review.
/// </summary>
/// <param name="SuggestionCount">How many fixed payments were suggested.</param>
/// <param name="ReviewCount">How many payees have an automatic category to confirm.</param>
public sealed record AttentionNotice(int SuggestionCount, int ReviewCount)
{
	#region Properties
	/// <summary>Gets the words about the suggestions, or <see langword="null"/> when there are none.</summary>
	public string? SuggestionText => SuggestionCount > 0 ? $"{SuggestionCount} possible fixed payment(s) found." : null;

	/// <summary>Gets the words about the payees to review, or <see langword="null"/> when there are none.</summary>
	public string? ReviewText => ReviewCount > 0 ? $"{ReviewCount} payee(s) with an automatic category to confirm." : null;
	#endregion
}
