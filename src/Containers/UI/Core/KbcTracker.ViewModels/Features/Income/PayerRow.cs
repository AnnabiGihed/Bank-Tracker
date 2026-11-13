namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payer in the earnings by payer of the Income tab: what it paid over the last twelve months and how
///              many times.
/// </summary>
/// <param name="Name">The payer's name.</param>
/// <param name="Total">What it paid in earnings.</param>
/// <param name="TotalText">The total, formatted.</param>
/// <param name="CountText">How many payments that was, in words.</param>
public sealed record PayerRow(string Name, decimal Total, string TotalText, string CountText);
