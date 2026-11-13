namespace KbcTracker.ViewModels.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The editor that divides a payment into categorised parts (ADR-0007). It keeps the parts the owner
///              types, tells how much is left to assign, checks what the backend will check (2 to 100 parts, each above
///              zero with a spending category, adding up to the payment to the cent) and builds the <c>split</c> and
///              <c>unsplit</c> requests. A new split starts with two parts; the first takes what the others leave.
/// </summary>
public sealed class SplitEditor
{
	#region Fields
	/// <summary>The formats of amounts.</summary>
	private readonly Formats _formats;

	/// <summary>The payment's amount in cents, without its sign.</summary>
	private readonly long _totalCents;

	/// <summary>The category new parts start with; empty when the payment's own category is not spending.</summary>
	private readonly string _defaultCategoryId;

	/// <summary>The parts, in the order the owner sees them; only this editor adds and removes.</summary>
	private readonly List<SplitPartEdit> _parts = [];

	/// <summary>Whether the first part is filled in from what the others leave, until the owner types in it.</summary>
	private bool _autoFirst;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="SplitEditor"/> for a payment.</summary>
	/// <param name="payment">The payment to split or whose split to edit.</param>
	/// <param name="categoryOptions">The categories a part can use: the spending categories in tree order.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public SplitEditor(TransactionRow payment, IReadOnlyList<CategoryOption> categoryOptions, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(payment);
		ArgumentNullException.ThrowIfNull(categoryOptions);
		ArgumentNullException.ThrowIfNull(formats);
		_formats = formats;
		SpendingCategories = categoryOptions;
		TransactionId = payment.Id;
		Name = payment.Name;
		_totalCents = (long)Math.Round(Math.Abs(payment.Amount) * 100, MidpointRounding.AwayFromZero);
		TotalText = formats.Money(Math.Abs(payment.Amount));
		_defaultCategoryId = payment.SplitDefaultCategoryId;
		IsSplitAlready = payment.SplitSeed is not null;
		_autoFirst = !IsSplitAlready;
		var seeds = payment.SplitSeed ?? [new(Math.Abs(payment.Amount), _defaultCategoryId, null), new(0, _defaultCategoryId, null)];
		foreach (var seed in seeds)
		{
			_parts.Add(new SplitPartEdit { Label = seed.Label ?? string.Empty, CategoryId = seed.CategoryId, AmountText = seed.Amount > 0 ? Formats.Plain(seed.Amount) : string.Empty });
		}

		Recalculate();
	}
	#endregion

	#region Properties
	/// <summary>Gets the payment being split.</summary>
	public string TransactionId { get; }

	/// <summary>Gets the payee's name, for the title.</summary>
	public string Name { get; }

	/// <summary>Gets the payment's amount without its sign, formatted, for the title.</summary>
	public string TotalText { get; }

	/// <summary>Gets the categories a part can use.</summary>
	public IReadOnlyList<CategoryOption> SpendingCategories { get; }

	/// <summary>Gets the parts, in the order the owner sees them; add and remove through <see cref="AddPart"/> and <see cref="RemovePart"/>.</summary>
	public IReadOnlyList<SplitPartEdit> Parts => _parts;

	/// <summary>Gets a value indicating whether the payment is split already, so its split can be removed.</summary>
	public bool IsSplitAlready { get; }

	/// <summary>Gets the part of the payment not assigned yet, in cents; negative when the parts add up to too much.</summary>
	public long LeftCents => _totalCents - Parts.Sum(p => p.Cents);

	/// <summary>Gets a value indicating whether the parts add up to the payment exactly.</summary>
	public bool IsBalanced => LeftCents == 0;

	/// <summary>Gets the words that tell how much is left to assign, or that everything is.</summary>
	public string LeftText => LeftCents switch
	{
		0 => "Everything is assigned",
		> 0 => "Left to assign: " + _formats.Money(LeftCents / 100m),
		_ => "Too much: " + _formats.Money(-LeftCents / 100m),
	};

	/// <summary>Gets a value indicating whether another part can be added.</summary>
	public bool CanAddPart => Parts.Count < PaymentSplit.MaxLines;

	/// <summary>Gets a value indicating whether the split can be saved: 2 to 100 parts, each above zero with a category
	/// and a label of at most 100 characters, adding up to the payment exactly.</summary>
	public bool IsValid => IsBalanced && Parts.Count is >= PaymentSplit.MinLines and <= PaymentSplit.MaxLines
		&& Parts.All(p => p.Cents > 0 && !string.IsNullOrEmpty(p.CategoryId) && p.Label.Trim().Length <= PaymentSplit.MaxLabelLength);
	#endregion

	#region Public Methods
	/// <summary>Adds an empty part with the payment's own category.</summary>
	/// <returns>The new part, or <see langword="null"/> when there are 100 parts already.</returns>
	public SplitPartEdit? AddPart()
	{
		if (!CanAddPart)
		{
			return null;
		}

		var part = new SplitPartEdit { CategoryId = _defaultCategoryId };
		_parts.Add(part);
		Recalculate();
		return part;
	}

	/// <summary>Removes a part.</summary>
	/// <param name="part">The part to remove.</param>
	/// <exception cref="ArgumentNullException"><paramref name="part"/> is <see langword="null"/>.</exception>
	public void RemovePart(SplitPartEdit part)
	{
		ArgumentNullException.ThrowIfNull(part);
		_parts.Remove(part);
		Recalculate();
	}

	/// <summary>Takes the amount the owner typed for a part. Typing in the first part stops it from taking what the others leave.</summary>
	/// <param name="part">The part.</param>
	/// <param name="amountText">The amount as typed.</param>
	/// <exception cref="ArgumentNullException"><paramref name="part"/> or <paramref name="amountText"/> is <see langword="null"/>.</exception>
	public void SetAmount(SplitPartEdit part, string amountText)
	{
		ArgumentNullException.ThrowIfNull(part);
		ArgumentNullException.ThrowIfNull(amountText);
		if (Parts.Count > 0 && ReferenceEquals(Parts[0], part))
		{
			_autoFirst = false;
		}

		part.AmountText = amountText;
		Recalculate();
	}

	/// <summary>Fills in the first part from what the others leave, while the owner has not typed in it. Call it after
	/// a part's category or amount changed through its own property.</summary>
	public void Recalculate()
	{
		if (!_autoFirst || Parts.Count == 0)
		{
			return;
		}

		var rest = _totalCents - Parts.Skip(1).Sum(p => p.Cents);
		Parts[0].AmountText = rest > 0 ? Formats.Plain(rest / 100m) : string.Empty;
	}

	/// <summary>Builds the request that saves the split.</summary>
	/// <returns>The <c>split</c> request, with each part's amount to the cent and its label trimmed.</returns>
	/// <exception cref="InvalidOperationException">The split is not valid.</exception>
	public DecisionRequest ToRequest() => IsValid
		? DecisionRequests.Split(TransactionId, [.. Parts.Select(p => new SplitPartRequest(p.Cents / 100m, p.CategoryId, string.IsNullOrWhiteSpace(p.Label) ? null : p.Label.Trim()))])
		: throw new InvalidOperationException("The parts must add up to the payment, each above zero and with a category.");

	/// <summary>Builds the request that removes the split.</summary>
	/// <returns>The <c>unsplit</c> request.</returns>
	public DecisionRequest UnsplitRequest() => DecisionRequests.Unsplit(TransactionId);
	#endregion
}
