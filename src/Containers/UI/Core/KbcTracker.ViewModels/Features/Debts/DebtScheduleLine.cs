namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One line of a debt's schedule as the expanded card shows it: the due date, the amount and where it stands.
/// </summary>
/// <param name="DueText">The due date, as <c>dd/MM/yy</c>.</param>
/// <param name="AmountText">The amount due, formatted.</param>
/// <param name="StateText">Paid, Partly paid with what is left, Overdue or Upcoming.</param>
/// <param name="Tone">How the state is coloured.</param>
public sealed record DebtScheduleLine(string DueText, string AmountText, string StateText, PillTone Tone);
