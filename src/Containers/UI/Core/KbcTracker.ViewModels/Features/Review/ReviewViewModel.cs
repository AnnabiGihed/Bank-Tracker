namespace KbcTracker.ViewModels.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the To review tab shows: the payees whose automatic category is not confirmed yet, filtered by the
///              direction of the money, with the request that confirms all payees shown at once.
/// </summary>
public sealed class ReviewViewModel
{
	#region Fields
	/// <summary>Every payee to review, in the analysis's order.</summary>
	private readonly IReadOnlyList<ReviewRow> _rows;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="ReviewViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="formats">The formats of amounts and dates.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public ReviewViewModel(Analysis analysis, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(formats);
		_rows = [.. analysis.Review.Select(r => new ReviewRow(r.SeriesKey, r.Name, r.Sample, r.Count, r.Total, formats.Money(r.Total), formats.ShortDate(r.Last),
			r.Incoming, r.Category.Id.Value, r.Category.Name))];
		CategoryGroups = CategoryOptions.Grouped(analysis, formats);
	}
	#endregion

	#region Properties
	/// <summary>Gets or sets which payees are shown, by the direction of their money.</summary>
	public DirectionFilter Direction { get; set; }

	/// <summary>Gets a value indicating whether there is nothing to review.</summary>
	public bool IsEmpty => _rows.Count == 0;

	/// <summary>Gets the categories a payee can be given, grouped by type.</summary>
	public IReadOnlyList<CategoryGroup> CategoryGroups { get; }

	/// <summary>Gets the payees the current filter shows.</summary>
	public IReadOnlyList<ReviewRow> VisibleRows => [.. _rows.Where(r => Direction switch
	{
		DirectionFilter.Out => !r.Incoming,
		DirectionFilter.In => r.Incoming,
		_ => true,
	})];

	/// <summary>Gets the message shown when nothing is left to review.</summary>
	public static string EmptyMessage => "All categories are confirmed. New payees will show up here after a sync.";

	/// <summary>Gets the explanation shown above the list.</summary>
	public static string Explanation => "These payees got a category automatically. Confirm it or pick another — it then applies to all their payments, past and future.";
	#endregion

	#region Public Methods
	/// <summary>Builds the request that confirms the category of every payee shown.</summary>
	/// <returns>The <c>confirm</c> request with all shown payees' keys.</returns>
	public DecisionRequest ConfirmShownRequest() => DecisionRequests.ConfirmMany(VisibleRows.Select(r => r.SeriesKey));

	/// <summary>Words the question asked before confirming every payee shown.</summary>
	/// <returns>The question, with the number of payees.</returns>
	public string ConfirmShownPrompt() => $"Confirm the category of {VisibleRows.Count} payees?";
	#endregion
}
