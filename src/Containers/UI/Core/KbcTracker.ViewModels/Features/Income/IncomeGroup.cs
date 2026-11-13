namespace KbcTracker.ViewModels.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A titled part of an Income section with its total and the payments that make it up, such as the
///              Salary payments among the earnings.
/// </summary>
/// <param name="Title">The group's name.</param>
/// <param name="TotalText">What its payments add up to, formatted.</param>
/// <param name="Rows">Its payments, newest first.</param>
public sealed record IncomeGroup(string Title, string TotalText, IReadOnlyList<IncomeRow> Rows);
