namespace KbcTracker.Domain.Features.Shared.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries an already classified analysis for display without requiring the bank's raw records.
/// </summary>
/// <param name="Items">The classified history.</param>
/// <param name="Categories">The category definitions.</param>
/// <param name="Fixed">The accepted fixed payments.</param>
/// <param name="Suggestions">The pending suggestions.</param>
/// <param name="Review">The unconfirmed payees.</param>
/// <param name="Months">The monthly totals.</param>
/// <param name="AvailableMonths">The selectable months.</param>
/// <param name="HolderName">The normalised holder name.</param>
/// <param name="Today">The analysis date.</param>
/// <param name="Debts">The status of every debt, or <see langword="null"/> for none.</param>
/// <param name="UnplannedLoans">The loan credits no debt tracks, or <see langword="null"/> for none.</param>
public sealed record AnalysisState(IReadOnlyList<TxnInfo> Items, IReadOnlyList<Category> Categories,
	IReadOnlyList<FixedRow> Fixed, IReadOnlyList<Suggestion> Suggestions, IReadOnlyList<ReviewItem> Review,
	IReadOnlyList<MonthSummary> Months, IReadOnlyList<string> AvailableMonths, string HolderName, DateOnly Today,
	IReadOnlyList<DebtStatus>? Debts = null, IReadOnlyList<TxnInfo>? UnplannedLoans = null);
