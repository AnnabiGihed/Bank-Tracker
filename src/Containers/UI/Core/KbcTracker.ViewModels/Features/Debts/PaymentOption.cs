namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One payment the owner can add to a debt as a repayment: a debit no debt counts yet.
/// </summary>
/// <param name="Id">The payment's transaction id.</param>
/// <param name="Text">What the picker shows: the date, the payee and the amount.</param>
public sealed record PaymentOption(string Id, string Text);
