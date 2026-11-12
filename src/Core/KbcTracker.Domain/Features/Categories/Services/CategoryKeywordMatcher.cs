namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Finds the category whose keyword occurs in a text. A keyword matches only at the start of a word (so
///              <c>"AD "</c> matches <c>"AD DELHAIZE"</c> but not <c>"AZAD "</c>), the longest keyword wins, and when
///              two keywords are equally long the deeper category wins because it is the more specific one. Used by the
///              analysis to classify payments and by the category upgrade to re-home decisions.
/// </summary>
public sealed class CategoryKeywordMatcher
{
	#region Fields
	/// <summary>
	/// Every keyword with its category, upper-cased, longest first and the deeper category first among equals.
	/// </summary>
	private readonly List<(string CategoryId, CategoryType Type, string Word)> _keywords;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="CategoryKeywordMatcher"/> over a category list.
	/// </summary>
	/// <param name="categories">The categories with their keywords.</param>
	/// <param name="tree">The tree of the same categories, which decides the depth of a category.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public CategoryKeywordMatcher(IEnumerable<Category> categories, CategoryTree tree)
	{
		ArgumentNullException.ThrowIfNull(categories);
		ArgumentNullException.ThrowIfNull(tree);
		_keywords = [.. categories
			.SelectMany(c => c.Keywords.Select(keyword => (CategoryId: c.Id.Value, c.Type, Word: keyword.ToUpperInvariant())))
			.Where(x => x.Word.Trim().Length > 0)
			.OrderByDescending(x => x.Word.Trim().Length).ThenByDescending(x => tree.Depth(x.CategoryId))];
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Finds the category of the longest keyword that starts a word of <paramref name="text"/> ("AUTOMOBILE" beats
	/// "CARREFOUR"); on equal length the deeper category wins.
	/// </summary>
	/// <param name="text">The text to search, such as a shop or payee name.</param>
	/// <param name="only">Searches only the categories of this type, or all of them when <see langword="null"/>.</param>
	/// <returns>The category id, or <see langword="null"/> when no keyword matches.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	public string? Find(string text, CategoryType? only = null)
	{
		ArgumentNullException.ThrowIfNull(text);
		var padded = " " + text.ToUpperInvariant() + " ";
		return _keywords.Where(x => only is null || x.Type == only)
			.Where(x => StartsAtWord(padded, x.Word))
			.Select(x => x.CategoryId).FirstOrDefault();
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Determines whether <paramref name="word"/> occurs in <paramref name="text"/> at the start of a word,
	/// so "AD " matches "AD DELHAIZE" but not "AZAD " or "RAD HUY". <paramref name="word"/> must not be empty.
	/// </summary>
	private static bool StartsAtWord(string text, string word)
	{
		var startsWithLetter = char.IsLetterOrDigit(word[0]);
		var index = text.IndexOf(word, StringComparison.Ordinal);
		while (index >= 0)
		{
			var isWordStart = index == 0 || !startsWithLetter || !char.IsLetterOrDigit(text[index - 1]);
			if (isWordStart)
			{
				return true;
			}

			index = text.IndexOf(word, index + 1, StringComparison.Ordinal);
		}

		return false;
	}
	#endregion
}
