using System.Globalization;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Errors;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.Splits.Errors;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Stages category-definition edits together with references in the user's decisions.
/// </summary>
internal static class CategoryDecisions
{
	#region Public Methods
	/// <summary>Creates a unique category or updates the selected existing definition, placing it in the tree.
	/// A sub-category takes the type of its top-level category; moving a category or changing a top-level type
	/// carries its sub-categories along.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="persistence">The shared decision unit of work.</param>
	/// <param name="request">The requested edit.</param>
	/// <returns>The stable identifier or a domain validation failure.</returns>
	public static Result<DecisionResponse> Save(WorkspaceData data, IWorkspacePersistence persistence, DecisionRequest request)
	{
		var name = request.Name ?? "";
		var keywords = (request.Keywords ?? "").Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var existing = data.Categories.FirstOrDefault(c => string.Equals(c.Id.Value, request.Id, StringComparison.Ordinal));
		var tree = new CategoryTree(data.Categories);
		var parentId = ParentOf(request, existing);
		var type = ResolveType(tree, request.Type, parentId);
		var placed = tree.Validate(existing?.Id.Value, parentId, type);
		if (placed.IsFailure)
		{
			return Result.Failure<DecisionResponse>(placed.Error, placed.ResultExceptionType);
		}

		var parent = parentId is null ? null : new CategoryId(parentId);
		if (existing is not null)
		{
			if (existing.Type != type && UsedBySplit(data, tree, existing))
			{
				return Result.Failure<DecisionResponse>(PaymentSplitErrors.CategoryUsedBySplit);
			}

			var changed = existing.Update(name, type, request.Color ?? existing.Color, keywords, parent);
			if (changed.IsFailure)
			{
				return Result.Failure<DecisionResponse>(changed.Error, changed.ResultExceptionType);
			}

			var followed = FollowType(tree, existing, type);
			return followed.IsSuccess
				? Result.Success(new DecisionResponse(existing.Id.Value))
				: Result.Failure<DecisionResponse>(followed.Error, followed.ResultExceptionType);
		}

		return Create(data, persistence, request, type, keywords, parent);
	}

	/// <summary>Removes a category: its payments, payee rules and fixed payments move to its parent (to <c>other</c>
	/// for a top-level category) and its sub-categories move up one level.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="persistence">The shared decision unit of work.</param>
	/// <param name="id">The selected category.</param>
	/// <returns>Success, or a missing/protected-category failure.</returns>
	public static Result<DecisionResponse> Delete(WorkspaceData data, IWorkspacePersistence persistence, string id)
	{
		var category = data.Categories.FirstOrDefault(c => string.Equals(c.Id.Value, id, StringComparison.Ordinal));
		if (category is null)
		{
			return Result.Failure<DecisionResponse>(CategoryErrors.NotFound, ResultExceptionType.NotFound);
		}

		var tree = new CategoryTree(data.Categories);
		var allowed = tree.ValidateDeletion(id);
		if (allowed.IsFailure)
		{
			return Result.Failure<DecisionResponse>(allowed.Error, allowed.ResultExceptionType);
		}

		var heir = tree.Parent(id)?.Id ?? CategoryId.Other;
		foreach (var child in tree.Children(id))
		{
			var moved = child.Update(child.Name, child.Type, child.Color, child.Keywords, category.ParentId);
			if (moved.IsFailure)
			{
				return Result.Failure<DecisionResponse>(moved.Error, moved.ResultExceptionType);
			}
		}

		persistence.RemoveCategory(category);
		data.User.ReassignCategory(category.Id, heir);
		return Result.Success(new DecisionResponse());
	}
	#endregion

	#region Private Helpers
	/// <summary>Reads the wanted parent: omitted keeps an existing category's parent (a new one is top level), empty
	/// makes it top level.</summary>
	private static string? ParentOf(DecisionRequest request, Category? existing)
	{
		if (request.ParentId is null)
		{
			return existing?.ParentId?.Value;
		}

		return request.ParentId.Length == 0 ? null : request.ParentId;
	}

	/// <summary>Chooses the type: a sub-category has its top-level category's type, so an omitted type is inherited and
	/// a different one is left for the tree to refuse; a top-level category uses the requested type, or Expense.</summary>
	private static CategoryType ResolveType(CategoryTree tree, string? requested, string? parentId)
	{
		var parsed = Enum.TryParse<CategoryType>(requested, out var type) ? type : (CategoryType?)null;
		var inherited = parentId is null ? null : tree.Root(parentId)?.Type;
		return parsed ?? inherited ?? CategoryType.Expense;
	}

	/// <summary>Tells whether a part of a split payment uses the category or one of its sub-categories; their type
	/// must then stay spending.</summary>
	private static bool UsedBySplit(WorkspaceData data, CategoryTree tree, Category category)
	{
		var ids = tree.Descendants(category.Id.Value).Select(d => d.Id).Append(category.Id).ToHashSet();
		return data.User.Splits.Values.Any(s => s.Lines.Any(l => ids.Contains(l.CategoryId)));
	}

	/// <summary>Gives every sub-category of a category the category's type, which they must share; stops at the first
	/// sub-category that refuses the change, so the edit is not saved half applied.</summary>
	private static Result FollowType(CategoryTree tree, Category category, CategoryType type)
	{
		foreach (var descendant in tree.Descendants(category.Id.Value).Where(d => d.Type != type))
		{
			var changed = descendant.Update(descendant.Name, type, descendant.Color, descendant.Keywords, descendant.ParentId);
			if (changed.IsFailure)
			{
				return changed;
			}
		}

		return Result.Success();
	}

	/// <summary>Allocates the same stable slug identifiers used by the legacy web app.</summary>
	private static Result<DecisionResponse> Create(WorkspaceData data, IWorkspacePersistence persistence, DecisionRequest request,
		CategoryType type, string[] keywords, CategoryId? parent)
	{
		var slug = CategoryDefaults.Slug(request.Name ?? "");
		if (slug.Length == 0)
		{
			return Result.Failure<DecisionResponse>(CategoryErrors.NameRequired);
		}

		var id = slug;
		var suffix = 2;
		while (data.Categories.Any(c => string.Equals(c.Id.Value, id, StringComparison.Ordinal)))
		{
			id = string.Create(CultureInfo.InvariantCulture, $"{slug}-{suffix++}");
		}

		var created = Category.Create(new CategoryId(id), request.Name ?? "", type, request.Color ?? Category.DefaultColor, keywords, builtIn: false, parent);
		if (created.IsFailure)
		{
			return Result.Failure<DecisionResponse>(created.Error, created.ResultExceptionType);
		}

		persistence.AddCategory(created.Value);
		return Result.Success(new DecisionResponse(created.Value.Id.Value));
	}
	#endregion
}
