using KbcTracker.Domain.Features.Overview.ValueObjects;
using KbcTracker.Domain.Features.Shared.Services;
using KbcTracker.Domain.Features.Shared.ValueObjects;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Transfers the backend's computed analysis so UI clients never classify incomplete bank data.
/// </summary>
/// <param name="Items">The classified history without raw records.</param>
/// <param name="Categories">The display category definitions.</param>
/// <param name="Fixed">The accepted fixed payments.</param>
/// <param name="Suggestions">The pending suggestions.</param>
/// <param name="Review">The unconfirmed payees.</param>
/// <param name="Months">The monthly totals.</param>
/// <param name="AvailableMonths">The month selector values.</param>
/// <param name="HolderName">The normalised account holder name.</param>
/// <param name="Today">The date used to compute the analysis.</param>
public sealed record AnalysisSnapshot(IReadOnlyList<TransactionAnalysisSnapshot> Items, IReadOnlyList<CategorySnapshot> Categories,
	IReadOnlyList<FixedPaymentSnapshot> Fixed, IReadOnlyList<SuggestionSnapshot> Suggestions, IReadOnlyList<ReviewSnapshot> Review,
	IReadOnlyList<MonthSummary> Months, IReadOnlyList<string> AvailableMonths, string HolderName, DateOnly Today)
{
	#region Public Methods
	/// <summary>Copies the complete analysis while omitting private bank source fields.</summary>
	/// <param name="analysis">The computed backend analysis.</param>
	/// <param name="today">The reference date of that analysis.</param>
	/// <returns>The transport snapshot.</returns>
	public static AnalysisSnapshot From(Analysis analysis, DateOnly today) => new(
		analysis.Items.Select(TransactionAnalysisSnapshot.From).ToArray(), analysis.CategoryList.Select(CategorySnapshot.From).ToArray(),
		analysis.Fixed.Select(FixedPaymentSnapshot.From).ToArray(), analysis.Suggestions.Select(SuggestionSnapshot.From).ToArray(),
		analysis.Review.Select(ReviewSnapshot.From).ToArray(), analysis.Months, analysis.AvailableMonths.ToArray(), analysis.HolderName, today);

	/// <summary>Restores the backend result for rendering without rerunning bank classification.</summary>
	/// <returns>The analysis with shared category and fixed-payment references.</returns>
	public Analysis ToAnalysis()
	{
		var categories = Categories.Select(c => c.ToDomain()).ToDictionary(c => c.Id.Value, StringComparer.Ordinal);
		var recurring = Fixed.Select(f => f.Definition.ToDomain()).ToDictionary(r => r.Id.Value, StringComparer.Ordinal);
		var items = Items.Select(i => i.ToDomain(categories, recurring)).ToDictionary(i => i.T.Id.Value, StringComparer.Ordinal);
		return Analysis.Rehydrate(new AnalysisState(items.Values.ToArray(), categories.Values.ToArray(),
			Fixed.Select(f => f.ToDomain(categories, items, recurring)).ToArray(), Suggestions.Select(s => s.ToDomain(items)).ToArray(),
			Review.Select(r => r.ToDomain(categories)).ToArray(), Months, AvailableMonths, HolderName, Today));
	}
	#endregion
}
