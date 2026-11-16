namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A loan credit, money in under Loan received or Borrowed from people, that no debt tracks yet; the Debts tab
///              lists it so the owner can say how it is repaid.
/// </summary>
/// <param name="Id">The credit's transaction id.</param>
/// <param name="DateText">The booking date, as <c>dd/MM/yy</c>.</param>
/// <param name="Payer">The payer's name.</param>
/// <param name="AmountText">The amount received, formatted.</param>
/// <param name="CategoryName">The name of the credit's category.</param>
public sealed record UnplannedCredit(string Id, string DateText, string Payer, string AmountText, string CategoryName);
