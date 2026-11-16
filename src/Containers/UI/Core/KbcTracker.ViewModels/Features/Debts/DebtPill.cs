namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The status of a debt as its pill shows it: the words (On track, Due soon, Overdue with the amount, Repaid,
///              Overpaid, No plan yet) and how they are coloured. The state is always written out, so the colour is a hint.
/// </summary>
/// <param name="Text">The words in the pill.</param>
/// <param name="Tone">How the pill is coloured.</param>
public sealed record DebtPill(string Text, PillTone Tone);
