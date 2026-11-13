namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One kind of waiting suggestion with the tab that lists it: fixed payments or subscriptions.
/// </summary>
/// <param name="TabId">The id of the tab that lists the suggestions, <c>fixed</c> or <c>subscriptions</c>.</param>
/// <param name="CountText">How many wait, in words, such as <c>2 fixed payments</c>.</param>
/// <param name="CheckText">The text of the link to the tab, such as <c>Check fixed payments</c>.</param>
public sealed record SuggestionLink(string TabId, string CountText, string CheckText);
