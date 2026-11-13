namespace KbcTracker.ViewModels.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The form that creates or edits a category: name, parent, type, colour and keywords. A built-in category
///              keeps its parent and type, a sub-category takes its root's type, and the form builds the
///              <c>saveCategory</c> and <c>deleteCategory</c> requests the way the old dialog did.
/// </summary>
public sealed class CategoryEdit
{
	#region Fields
	/// <summary>The type the category had when the form opened; a built-in category is saved with it.</summary>
	private readonly CategoryType _originalType;

	/// <summary>The name of the parent the category had when the form opened, or <see langword="null"/> for a top-level one.</summary>
	private readonly string? _originalParentName;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="CategoryEdit"/>.</summary>
	/// <param name="id">The category being edited, or <see langword="null"/> for a new one.</param>
	/// <param name="title">The title of the form.</param>
	/// <param name="name">The name.</param>
	/// <param name="type">The type.</param>
	/// <param name="parentId">The parent, or an empty string for top level.</param>
	/// <param name="color">The colour.</param>
	/// <param name="keywords">The keywords, comma separated.</param>
	/// <param name="builtIn">Whether the category is built in.</param>
	/// <param name="parentOptions">The categories it can be moved under: not itself or anything below it, and not so deep that its own sub-categories would pass the deepest level.</param>
	/// <param name="originalParentName">The name of its current parent, or <see langword="null"/> for none.</param>
	internal CategoryEdit(string? id, string title, string name, CategoryType type, string parentId, string color, string keywords, bool builtIn,
		IReadOnlyList<CategoryOption> parentOptions, string? originalParentName)
	{
		Id = id;
		Title = title;
		Name = name;
		Type = type;
		ParentId = parentId;
		Color = color;
		Keywords = keywords;
		BuiltIn = builtIn;
		ParentOptions = parentOptions;
		_originalType = type;
		_originalParentName = originalParentName;
	}
	#endregion

	#region Properties
	/// <summary>Gets the category being edited, or <see langword="null"/> for a new one.</summary>
	public string? Id { get; }

	/// <summary>Gets the title of the form: new category, new sub-category or edit category.</summary>
	public string Title { get; }

	/// <summary>Gets or sets the name.</summary>
	public string Name { get; set; }

	/// <summary>Gets or sets the type, which only a top-level category chooses itself.</summary>
	public CategoryType Type { get; set; }

	/// <summary>Gets or sets the parent's id; an empty string for a top-level category.</summary>
	public string ParentId { get; set; }

	/// <summary>Gets or sets the colour, as <c>#rrggbb</c>.</summary>
	public string Color { get; set; }

	/// <summary>Gets or sets the keywords, comma separated: payments whose shop or payee name contains one get this category automatically.</summary>
	public string Keywords { get; set; }

	/// <summary>Gets a value indicating whether the category is built in, so its parent and type are fixed.</summary>
	public bool BuiltIn { get; }

	/// <summary>Gets the categories the category can be moved under.</summary>
	public IReadOnlyList<CategoryOption> ParentOptions { get; }

	/// <summary>Gets a value indicating whether the category is new.</summary>
	public bool IsNew => Id is null;

	/// <summary>Gets a value indicating whether the parent can be chosen.</summary>
	public bool ShowParent => !BuiltIn;

	/// <summary>Gets a value indicating whether the type can be chosen: only for a top-level category that is not built in.</summary>
	public bool ShowType => !BuiltIn && string.IsNullOrEmpty(ParentId);

	/// <summary>Gets a value indicating whether the category can be deleted: it exists and is not built in.</summary>
	public bool CanDelete => !IsNew && !BuiltIn;

	/// <summary>Gets a value indicating whether the form can be saved: it needs a name.</summary>
	public bool IsValid => !string.IsNullOrWhiteSpace(Name);
	#endregion

	#region Public Methods
	/// <summary>Builds the request that saves the form.</summary>
	/// <returns>The <c>saveCategory</c> request.</returns>
	/// <exception cref="InvalidOperationException">The form is not valid.</exception>
	public DecisionRequest ToRequest()
	{
		if (!IsValid)
		{
			throw new InvalidOperationException("The category needs a name.");
		}

		var type = BuiltIn ? _originalType.ToString() : OwnTypeName();
		return DecisionRequests.SaveCategory(Id, Name, type, BuiltIn ? null : ParentId, Color, Keywords);
	}

	/// <summary>Words the question asked before deleting the category: where its payments and rules go.</summary>
	/// <returns>The question.</returns>
	public string DeletePrompt() => "Delete this category? Its payments and rules move to "
		+ (_originalParentName is null
			? "“Other”; its payee rules show up in To review again."
			: $"its parent, “{_originalParentName}”.");

	/// <summary>Builds the request that deletes the category.</summary>
	/// <returns>The <c>deleteCategory</c> request.</returns>
	/// <exception cref="InvalidOperationException">The category is new or built in.</exception>
	public DecisionRequest DeleteRequest() => CanDelete
		? DecisionRequests.DeleteCategory(Id!)
		: throw new InvalidOperationException("A new or built-in category cannot be deleted.");
	#endregion

	#region Private Helpers
	/// <summary>Names the type of a category that is not built in: its own when it is top level, otherwise empty, because a
	/// sub-category takes its root's type.</summary>
	private string OwnTypeName() => string.IsNullOrEmpty(ParentId) ? Type.ToString() : string.Empty;
	#endregion
}
