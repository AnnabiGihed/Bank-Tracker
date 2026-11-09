namespace KbcTracker.Domain.Features.Review.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A payee whose automatic category the user has not confirmed yet, shown on the To review tab.
/// </summary>
/// <param name="SeriesKey">The payee series.</param>
/// <param name="Name">The payee name of its latest transaction.</param>
/// <param name="Category">The automatic category of its latest transaction.</param>
/// <param name="Count">The number of unconfirmed transactions.</param>
/// <param name="Total">Their sum (negative for money out).</param>
/// <param name="Last">The date of the latest one.</param>
/// <param name="Incoming">Whether the latest one is money in.</param>
/// <param name="Sample">The description of the latest one, to help the user recognise the payee.</param>
public sealed record ReviewItem(string SeriesKey, string Name, Category Category, int Count, decimal Total, DateOnly Last, bool Incoming, string Sample);
