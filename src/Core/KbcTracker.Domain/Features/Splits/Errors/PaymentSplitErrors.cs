namespace KbcTracker.Domain.Features.Splits.Errors;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors splitting a payment can fail with, coded <c>PaymentSplit.Property.Reason</c>.
/// </summary>
public static class PaymentSplitErrors
{
	#region Static Instances
	/// <summary>
	/// A split has fewer than two or more than a hundred lines.
	/// </summary>
	public static readonly Error LineCount = new("PaymentSplit.Lines.Count", "A split needs between 2 and 100 parts.");

	/// <summary>
	/// A part's amount is not positive or has more than two decimals.
	/// </summary>
	public static readonly Error AmountInvalid = new("PaymentSplit.Amount.Invalid", "Every part needs a positive amount with at most two decimals.");

	/// <summary>
	/// A part's label is longer than a hundred characters.
	/// </summary>
	public static readonly Error LabelTooLong = new("PaymentSplit.Label.TooLong", "A part's label can have at most 100 characters.");

	/// <summary>
	/// The parts do not add up to the payment's amount.
	/// </summary>
	public static readonly Error SumMismatch = new("PaymentSplit.Amount.Mismatch", "The parts must add up to the payment's amount.");

	/// <summary>
	/// A part uses a category that is not a spending category.
	/// </summary>
	public static readonly Error NotExpenseCategory = new("PaymentSplit.Category.NotExpense", "A payment can only be split into spending categories.");

	/// <summary>
	/// The payment has no split to remove.
	/// </summary>
	public static readonly Error NotFound = new("PaymentSplit.NotFound", "This payment is not split.");

	/// <summary>
	/// The payment is not spending (income, savings, an own transfer…), so it cannot be split into spending parts.
	/// </summary>
	public static readonly Error NotSpendingPayment = new("PaymentSplit.Payment.NotSpending", "Only spending payments and refunds can be split.");

	/// <summary>
	/// A category's type cannot change while parts of split payments use it or one of its sub-categories, because
	/// those parts must stay spending.
	/// </summary>
	public static readonly Error CategoryUsedBySplit = new("PaymentSplit.Category.InUse", "Parts of split payments use this category; move them to another category before changing what it counts as.");
	#endregion
}
