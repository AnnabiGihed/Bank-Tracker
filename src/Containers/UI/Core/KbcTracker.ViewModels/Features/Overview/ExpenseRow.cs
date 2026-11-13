namespace KbcTracker.ViewModels.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : One of the month's biggest expenses on the Overview tab.
/// </summary>
/// <param name="DateText">The booking date, as <c>dd/MM</c>.</param>
/// <param name="Name">The payee's name.</param>
/// <param name="CategoryName">The category's name, or <see langword="null"/> for a split payment.</param>
/// <param name="CategoryColor">The category's colour, or <see langword="null"/> for a split payment.</param>
/// <param name="PartCount">The number of parts of a split payment, otherwise zero.</param>
/// <param name="AmountText">The amount spent, formatted.</param>
public sealed record ExpenseRow(string DateText, string Name, string? CategoryName, string? CategoryColor, int PartCount, string AmountText)
{
	#region Properties
	/// <summary>Gets a value indicating whether the payment is split into parts.</summary>
	public bool IsSplit => PartCount > 0;

	/// <summary>Gets the words of the tag next to the name: the number of parts of a split payment, otherwise the category.</summary>
	public string TagText => IsSplit ? $"Split into {PartCount} parts" : CategoryName ?? string.Empty;
	#endregion
}
