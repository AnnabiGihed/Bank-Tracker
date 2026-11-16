namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One <c>LOAN:</c> series of bank loan instalments a bank debt can be linked to.
/// </summary>
/// <param name="Key">The series key, <c>LOAN:</c> followed by the loan's reference.</param>
/// <param name="Text">What the picker shows: the payee and the reference.</param>
public sealed record LoanSeriesChoice(string Key, string Text);
