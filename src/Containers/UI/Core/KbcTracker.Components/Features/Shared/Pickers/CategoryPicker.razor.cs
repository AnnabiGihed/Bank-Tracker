using System.Globalization;

namespace KbcTracker.Components.Features.Shared.Pickers;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A searchable drop-down over category options in tree order, top-level categories as bold headings and the others indented by level, used wherever the owner
///              picks a category: a transaction, a payee to review, a fixed payment, a split part, a parent category.
///              The accessible name is required because the drop-down has no visible label of its own in a table row.
/// </summary>
public partial class CategoryPicker : ComponentBase
{
	#region Parameters
	/// <summary>Gets or sets the categories to choose from.</summary>
	[Parameter]
	public IEnumerable<CategoryOption> Options { get; set; } = [];

	/// <summary>Gets or sets the id of the chosen category, or <see langword="null"/> or empty for none.</summary>
	[Parameter]
	public string? Value { get; set; }

	/// <summary>Gets or sets what happens when the owner chooses a category; it receives the category id, or an empty string when cleared.</summary>
	[Parameter]
	public EventCallback<string> ValueChanged { get; set; }

	/// <summary>Gets or sets the accessible name of the drop-down, for example "Category of Delhaize".</summary>
	[Parameter]
	public string AriaLabel { get; set; } = "Category";

	/// <summary>Gets or sets the text shown while nothing is chosen.</summary>
	[Parameter]
	public string? Placeholder { get; set; }

	/// <summary>Gets or sets a value indicating whether the owner may clear the choice.</summary>
	[Parameter]
	public bool AllowClear { get; set; }

	/// <summary>Gets or sets a stable test id for the browser test, or <see langword="null"/> when the accessible name is enough.</summary>
	[Parameter]
	public string? TestId { get; set; }
	#endregion

	#region Fields
	/// <summary>The attributes of the drop-down's input: its accessible name.</summary>
	private IReadOnlyDictionary<string, object> _inputAttributes = new Dictionary<string, object>(StringComparer.Ordinal);
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnParametersSet() =>
		_inputAttributes = new Dictionary<string, object>(StringComparer.Ordinal) { ["aria-label"] = AriaLabel };
	#endregion

	#region Private Helpers
	/// <summary>Gets the style class of an option: a heading for a top-level category, an indented entry otherwise.</summary>
	/// <param name="option">The option.</param>
	/// <returns>The class name.</returns>
	private static string ItemClass(CategoryOption option) => option.IsRoot ? "picker-root" : "picker-child";

	/// <summary>Gives the stylesheet the level of an option, so it can indent by level.</summary>
	/// <param name="option">The option.</param>
	/// <returns>The inline custom property.</returns>
	private static string ItemStyle(CategoryOption option) => string.Create(CultureInfo.InvariantCulture, $"--depth: {option.Depth}");
	#endregion

	#region Event Handlers
	/// <summary>Passes the chosen category id on.</summary>
	/// <param name="chosen">The id the drop-down reports, or <see langword="null"/> when cleared.</param>
	/// <returns>A task that completes when the parent handled the choice.</returns>
	private Task OnChangeAsync(object? chosen) => ValueChanged.InvokeAsync(chosen as string ?? string.Empty);
	#endregion
}
