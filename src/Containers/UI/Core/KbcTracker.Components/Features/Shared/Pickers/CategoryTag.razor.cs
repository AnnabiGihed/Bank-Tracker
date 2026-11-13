namespace KbcTracker.Components.Features.Shared.Pickers;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A small tag with a category's name and its colour as a dot. The name always appears, so the colour never
///              carries the meaning alone.
/// </summary>
public partial class CategoryTag : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the text of the tag, usually the category's name.</summary>
	[Parameter]
	public string Text { get; set; } = string.Empty;

	/// <summary>Gets or sets the colour of the dot, as a CSS colour; <see langword="null"/> keeps the stylesheet's neutral colour.</summary>
	[Parameter]
	public string? Color { get; set; }
	#endregion

	#region Private Helpers
	/// <summary>Gets the inline style that paints the dot, or <see langword="null"/> to leave it to the stylesheet.</summary>
	private string? DotStyle => Color is null ? null : $"background: {Color}";
	#endregion
}
