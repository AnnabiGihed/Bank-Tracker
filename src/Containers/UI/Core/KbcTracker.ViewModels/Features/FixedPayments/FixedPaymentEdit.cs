namespace KbcTracker.ViewModels.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The form that edits a fixed payment: name, category, usual amount and rhythm. It checks what the
///              form needs and builds the <c>updateFixed</c> request.
/// </summary>
public sealed class FixedPaymentEdit
{
	#region Static Instances
	/// <summary>The rhythms the form offers, with their length in days.</summary>
	public static readonly IReadOnlyList<(int Days, string Label)> Rhythms = [(30, "Monthly"), (91, "Quarterly"), (365, "Yearly")];
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="FixedPaymentEdit"/> for an existing fixed payment.</summary>
	/// <param name="id">The fixed payment.</param>
	/// <param name="name">Its name.</param>
	/// <param name="categoryId">Its category.</param>
	/// <param name="amount">Its usual amount.</param>
	/// <param name="cadenceDays">Its rhythm in days, which the form rounds to monthly, quarterly or yearly.</param>
	/// <exception cref="ArgumentNullException"><paramref name="id"/>, <paramref name="name"/> or <paramref name="categoryId"/> is <see langword="null"/>.</exception>
	public FixedPaymentEdit(string id, string name, string categoryId, decimal amount, int cadenceDays)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(name);
		ArgumentNullException.ThrowIfNull(categoryId);
		Id = id;
		Name = name;
		CategoryId = categoryId;
		Amount = amount;
		Rhythm = RhythmOf(cadenceDays);
	}
	#endregion

	#region Properties
	/// <summary>Gets the fixed payment being edited.</summary>
	public string Id { get; }

	/// <summary>Gets or sets the name.</summary>
	public string Name { get; set; }

	/// <summary>Gets or sets the category id.</summary>
	public string CategoryId { get; set; }

	/// <summary>Gets or sets the usual amount, or <see langword="null"/> while the field is empty.</summary>
	public decimal? Amount { get; set; }

	/// <summary>Gets or sets the rhythm in days: 30, 91 or 365.</summary>
	public int Rhythm { get; set; }

	/// <summary>Gets a value indicating whether the form can be saved: a name, a category and an amount of zero or more.</summary>
	public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(CategoryId) && Amount is >= 0;
	#endregion

	#region Public Methods
	/// <summary>Rounds a rhythm in days to monthly (30), quarterly (91) or yearly (365).</summary>
	/// <param name="cadenceDays">The rhythm in days.</param>
	/// <returns>The rhythm the form offers that is closest in kind.</returns>
	public static int RhythmOf(int cadenceDays)
	{
		if (cadenceDays > 200)
		{
			return 365;
		}

		return cadenceDays > 40 ? 91 : 30;
	}

	/// <summary>Builds the request that saves the form.</summary>
	/// <returns>The <c>updateFixed</c> request.</returns>
	/// <exception cref="InvalidOperationException">The form is not valid.</exception>
	public DecisionRequest ToRequest() => IsValid
		? DecisionRequests.UpdateFixed(Id, Name, CategoryId, Amount!.Value, Rhythm)
		: throw new InvalidOperationException("The fixed payment needs a name, a category and an amount of zero or more.");
	#endregion
}
