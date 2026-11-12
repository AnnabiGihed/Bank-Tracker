namespace KbcTracker.Domain.Features.Splits.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How the user divided one payment over categories: two to a hundred <see cref="SplitLine"/>s whose
///              amounts add up to the payment's amount (ADR-0007).
///
///              Immutable. A stored split is read back as it is, so it can differ from its payment after the bank
///              changed the amount; the analysis then shows the gap as a part of its own.
/// </summary>
public sealed class PaymentSplit
{
	#region Constants
	/// <summary>
	/// The fewest lines a split has.
	/// </summary>
	public const int MinLines = 2;

	/// <summary>
	/// The most lines a split has; receipts with more lines are combined per category before they are stored.
	/// </summary>
	public const int MaxLines = 100;

	/// <summary>
	/// The most characters a label has.
	/// </summary>
	public const int MaxLabelLength = 100;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a split from lines that are already valid or stored.
	/// </summary>
	/// <param name="lines">The lines.</param>
	private PaymentSplit(IReadOnlyList<SplitLine> lines) => Lines = lines;
	#endregion

	#region Properties
	/// <summary>
	/// Gets the parts of the payment, in the order the user gave them.
	/// </summary>
	public IReadOnlyList<SplitLine> Lines { get; }
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates a split of a payment, checking that the parts add up to it.
	/// </summary>
	/// <param name="paymentAmount">The payment's amount; its sign is ignored.</param>
	/// <param name="lines">The parts; labels are trimmed and a blank label becomes <see langword="null"/>.</param>
	/// <returns>The split, or <see cref="PaymentSplitErrors.LineCount"/>, <see cref="PaymentSplitErrors.AmountInvalid"/>,
	/// <see cref="PaymentSplitErrors.LabelTooLong"/> or <see cref="PaymentSplitErrors.SumMismatch"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
	public static Result<PaymentSplit> Create(decimal paymentAmount, IEnumerable<SplitLine> lines)
	{
		ArgumentNullException.ThrowIfNull(lines);
		var cleaned = new List<SplitLine>();
		foreach (var line in lines)
		{
			if (line is null)
			{
				return Result.Failure<PaymentSplit>(PaymentSplitErrors.AmountInvalid);
			}

			var label = string.IsNullOrWhiteSpace(line.Label) ? null : line.Label.Trim();
			if (label is { Length: > MaxLabelLength })
			{
				return Result.Failure<PaymentSplit>(PaymentSplitErrors.LabelTooLong);
			}

			if (line.Amount <= 0 || decimal.Round(line.Amount, 2) != line.Amount)
			{
				return Result.Failure<PaymentSplit>(PaymentSplitErrors.AmountInvalid);
			}

			cleaned.Add(line with { Label = label });
		}

		if (cleaned.Count is < MinLines or > MaxLines)
		{
			return Result.Failure<PaymentSplit>(PaymentSplitErrors.LineCount);
		}

		if (cleaned.Sum(l => l.Amount) != Math.Abs(paymentAmount))
		{
			return Result.Failure<PaymentSplit>(PaymentSplitErrors.SumMismatch);
		}

		return new PaymentSplit(cleaned.AsReadOnly());
	}

	/// <summary>
	/// Rebuilds a stored split without the checks: stored decisions are kept as they are.
	/// </summary>
	/// <param name="lines">The stored lines.</param>
	/// <returns>The split.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
	public static PaymentSplit Rehydrate(IEnumerable<SplitLine> lines)
	{
		ArgumentNullException.ThrowIfNull(lines);
		return new PaymentSplit(lines.ToList().AsReadOnly());
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Moves the lines of one category to another, used when a category is deleted or merged.
	/// </summary>
	/// <param name="from">The category being removed.</param>
	/// <param name="to">The category that takes over.</param>
	/// <returns>A split with the lines moved, or this split when no line uses <paramref name="from"/>.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public PaymentSplit ReassignCategory(CategoryId from, CategoryId to)
	{
		ArgumentNullException.ThrowIfNull(from);
		ArgumentNullException.ThrowIfNull(to);
		return Lines.Any(l => l.CategoryId == from)
			? new PaymentSplit(Lines.Select(l => l.CategoryId == from ? l with { CategoryId = to } : l).ToList().AsReadOnly())
			: this;
	}
	#endregion
}
