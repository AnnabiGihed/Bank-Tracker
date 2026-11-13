namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The suggestions that wait for the owner, split in the fixed payments and the subscriptions, with the words
///              that say so and a link to each tab that has any. The Overview's notice and its fixed payments card share it.
/// </summary>
/// <param name="FixedCount">How many fixed payments were suggested.</param>
/// <param name="SubscriptionCount">How many subscriptions were suggested.</param>
public sealed record WaitingSuggestions(int FixedCount, int SubscriptionCount)
{
	#region Properties
	/// <summary>Gets how many suggestions wait in all.</summary>
	public int Total => FixedCount + SubscriptionCount;

	/// <summary>Gets a link to each tab that has suggestions, fixed payments first.</summary>
	public IReadOnlyList<SuggestionLink> Links
	{
		get
		{
			var links = new List<SuggestionLink>();
			if (FixedCount > 0)
			{
				links.Add(new("fixed", FixedCount == 1 ? "1 fixed payment" : $"{FixedCount} fixed payments", "Check fixed payments"));
			}

			if (SubscriptionCount > 0)
			{
				links.Add(new("subscriptions", SubscriptionCount == 1 ? "1 subscription" : $"{SubscriptionCount} subscriptions", "Check subscriptions"));
			}

			return links;
		}
	}

	/// <summary>Gets the words that tell what waits, such as <c>2 fixed payments and 1 subscription waiting</c>, or <see langword="null"/> when nothing does.</summary>
	public string? Text => Total > 0 ? $"{string.Join(" and ", Links.Select(l => l.CountText))} {WaitingWord}" : null;

	/// <summary>Gets the word that follows the counts and says they wait for the owner.</summary>
	public static string WaitingWord => "waiting";
	#endregion
}
