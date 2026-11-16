namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Conversions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One debt as stored in the <c>Debts</c> JSON column. <paramref name="Version"/> names the shape, so a later
///              change can read what an older version wrote; the kind and frequency are stored by name, never by number.
/// </summary>
/// <param name="Version">The shape of this record; 1 had no <paramref name="BorrowedOn"/>, 2 adds it.</param>
/// <param name="Id">The debt's identifier.</param>
/// <param name="Kind">The name of the <see cref="DebtKind"/>.</param>
/// <param name="Lender">The lender's name.</param>
/// <param name="Iban">The lender's IBAN, or <see langword="null"/>.</param>
/// <param name="ReceivedPaymentId">The credit that paid the money out, or <see langword="null"/>.</param>
/// <param name="Principal">The amount received.</param>
/// <param name="SeriesKey">The <c>LOAN:</c> series, or <see langword="null"/>.</param>
/// <param name="Plan">The repayment plan, or <see langword="null"/> when there is none yet.</param>
/// <param name="Included">The payments added by hand.</param>
/// <param name="Excluded">The payments excluded by hand.</param>
/// <param name="BorrowedOn">The day the money was received; missing in version 1, which then reads as the plan's first date.</param>
internal sealed record StoredDebt(int Version, string Id, string Kind, string Lender, string? Iban, string? ReceivedPaymentId, decimal Principal,
	string? SeriesKey, StoredRepaymentPlan? Plan, string[] Included, string[] Excluded, DateOnly? BorrowedOn = null);
