namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How a month's spending in a category compares with the average of the previous months, in words.
/// </summary>
/// <param name="Kind">Which way it differs, if at all.</param>
/// <param name="Text">The words: the difference as a whole amount, "similar", or a dash for none.</param>
public sealed record CategoryComparison(ComparisonKind Kind, string Text)
{
	#region Factory Methods
	/// <summary>Compares a month's spending with the average of the previous months: higher or lower when it differs
	/// by more than 15 percent, similar otherwise, and none when there is nothing to compare or the category is not
	/// spending.</summary>
	/// <param name="value">The month's total, sub-categories included.</param>
	/// <param name="average">The average of the previous months, or <see langword="null"/> without any.</param>
	/// <param name="type">How the category counts in the totals.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>The comparison.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="formats"/> is <see langword="null"/>.</exception>
	public static CategoryComparison Of(decimal value, decimal? average, CategoryType type, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(formats);
		if (average is null || average == 0 || type != CategoryType.Expense)
		{
			return new(ComparisonKind.None, "—");
		}

		if (value > average * 1.15m)
		{
			return new(ComparisonKind.Higher, $"+{formats.WholeMoney(value - average.Value)}");
		}

		return value < average * 0.85m
			? new(ComparisonKind.Lower, $"−{formats.WholeMoney(average.Value - value)}")
			: new(ComparisonKind.Similar, "similar");
	}
	#endregion
}
