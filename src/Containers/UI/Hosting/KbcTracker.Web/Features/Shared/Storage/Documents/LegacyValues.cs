using System.Security.Cryptography;
using System.Text;

namespace KbcTracker.Web.Features.Shared.Storage.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Reads values that older versions could store but the domain no longer accepts, so one such record
///              never makes a whole data file unreadable: a blank id gets a stable id derived from the record's
///              content, and a blank category id keeps its old meaning, the "Other" category.
/// </summary>
internal static class LegacyValues
{
	#region Public Methods
	/// <summary>
	/// Returns the stored id, or a stable one derived from the record's content when it is blank, so the same
	/// record gets the same id on every load.
	/// </summary>
	/// <param name="id">The stored id.</param>
	/// <param name="content">The record's fields that identify it.</param>
	/// <returns>The id to use; never blank.</returns>
	public static string IdOrDerived(string? id, params string?[] content) =>
		string.IsNullOrWhiteSpace(id)
			? "legacy-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', content))))[..20].ToLowerInvariant()
			: id;

	/// <summary>
	/// Reads a stored category reference: <see langword="null"/> stays "no category", and a blank id, which older
	/// versions showed as "Other", becomes <see cref="CategoryId.Other"/>.
	/// </summary>
	/// <param name="categoryId">The stored category id.</param>
	/// <returns>The category, or <see langword="null"/>.</returns>
	public static CategoryId? Category(string? categoryId) => categoryId switch
	{
		null => null,
		_ when string.IsNullOrWhiteSpace(categoryId) => CategoryId.Other,
		_ => new CategoryId(categoryId),
	};
	#endregion
}
