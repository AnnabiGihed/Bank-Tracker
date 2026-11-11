using System.Globalization;
using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Errors;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Categories.ValueObjects;
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
	/// <summary>Creates a unique category or updates the selected existing definition.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="persistence">The shared decision unit of work.</param>
	/// <param name="request">The requested edit.</param>
	/// <returns>The stable identifier or a domain validation failure.</returns>
	public static Result<DecisionResponse> Save(WorkspaceData data, IWorkspacePersistence persistence, DecisionRequest request)
	{
		var name = request.Name ?? "";
		var type = Enum.TryParse<CategoryType>(request.Type, out var parsed) ? parsed : CategoryType.Expense;
		var keywords = (request.Keywords ?? "").Split([',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		var existing = data.Categories.FirstOrDefault(c => string.Equals(c.Id.Value, request.Id, StringComparison.Ordinal));
		if (existing is not null)
		{
			var changed = existing.Update(name, type, request.Color ?? existing.Color, keywords);
			return changed.IsSuccess ? Result.Success(new DecisionResponse(existing.Id.Value))
				: Result.Failure<DecisionResponse>(changed.Error, changed.ResultExceptionType);
		}

		return Create(data, persistence, request, type, keywords);
	}

	/// <summary>Removes a category and all references to it in one pending decision.</summary>
	/// <param name="data">The tracked workspace.</param>
	/// <param name="persistence">The shared decision unit of work.</param>
	/// <param name="id">The selected category.</param>
	/// <returns>Success, or a missing/protected-category failure.</returns>
	public static Result<DecisionResponse> Delete(WorkspaceData data, IWorkspacePersistence persistence, string id)
	{
		var category = data.Categories.FirstOrDefault(c => string.Equals(c.Id.Value, id, StringComparison.Ordinal));
		if (category is null)
		{
			return Result.Failure<DecisionResponse>(new Error("Category.NotFound", "This category does not exist."), ResultExceptionType.NotFound);
		}

		if (category.Id == CategoryId.Other)
		{
			return Result.Failure<DecisionResponse>(CategoryErrors.OtherCannotBeDeleted);
		}

		persistence.RemoveCategory(category);
		data.User.ForgetCategory(category.Id);
		return Result.Success(new DecisionResponse());
	}
	#endregion

	#region Private Helpers
	/// <summary>Allocates the same stable slug identifiers used by the legacy web app.</summary>
	private static Result<DecisionResponse> Create(WorkspaceData data, IWorkspacePersistence persistence, DecisionRequest request,
		CategoryType type, string[] keywords)
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

		var created = Category.Create(new CategoryId(id), request.Name ?? "", type, request.Color ?? Category.DefaultColor, keywords, builtIn: false);
		if (created.IsFailure)
		{
			return Result.Failure<DecisionResponse>(created.Error, created.ResultExceptionType);
		}

		persistence.AddCategory(created.Value);
		return Result.Success(new DecisionResponse(created.Value.Id.Value));
	}
	#endregion
}
