using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Shared.Services;
using Pivot.Framework.Domain.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Turns the flat category list that older databases were seeded with into the default category tree,
///              keeping what the owner decided. Runs from <c>--initialize</c>, so on every start of the application.
///              It acts only while no category has a parent and at least one of the three flat defaults that are
///              merged (<c>transport-fuel</c>, <c>subscriptions</c>, <c>utilities-telecom</c>) still exists; a tree
///              the owner flattened afterwards is therefore never seeded again.
///
///              In one transaction it adds the tree nodes that are missing, attaches the kept ids (<c>groceries</c>,
///              <c>car</c>…) to their place in the tree, and merges the three flat defaults into the tree. Every payee
///              rule, payment choice and fixed payment that pointed to a merged category moves to the category whose
///              keyword matches its payee (the same matching the analysis uses), or to a fallback node when none
///              does; a payee rule moved this way becomes unconfirmed so it shows up in To review, while payment
///              choices and fixed payments keep their state. The keywords of a merged category the owner edited are
///              added to its fallback node. A kept category that is still exactly the old default also receives the
///              new name, colour and keywords; one the owner edited keeps its own and is only attached. A kept
///              category always takes the tree's type, because a sub-category has the type of its top-level
///              category. Categories the owner created stay top level, unchanged.
/// </summary>
/// <param name="dbContext">The context whose categories and decisions are changed.</param>
/// <param name="unitOfWork">Saves everything in one transaction.</param>
public sealed class CategoryTreeUpgrade(KbcTrackerDbContext dbContext, IUnitOfWork unitOfWork)
{
	#region Static Instances
	/// <summary>
	/// The old flat defaults without a single counterpart in the tree, and the node that takes over a reference
	/// whose payee matches no keyword.
	/// </summary>
	private static readonly Dictionary<string, string> Fallbacks = new(StringComparer.Ordinal)
	{
		["transport-fuel"] = "transport",
		["subscriptions"] = "rec-streaming",
		["utilities-telecom"] = "bills",
	};
	#endregion

	#region Public Methods
	/// <summary>
	/// Upgrades the flat category list to the default tree when the tree is absent.
	/// </summary>
	/// <param name="country">The ISO country code that selects the keyword pack of the new nodes.</param>
	/// <param name="cancellationToken">Cancels the upgrade.</param>
	/// <returns>Whether anything was changed (<see langword="false"/> when the tree already exists, no merged flat
	/// default is left, or the database is not initialised), or the unit of work's save error.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="country"/> is <see langword="null"/>.</exception>
	public async Task<Result<bool>> UpgradeAsync(string country, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(country);
		var stored = await dbContext.Categories.ToListAsync(cancellationToken).ConfigureAwait(false);
		var user = await dbContext.UserData.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		if (user is null || stored.Exists(c => c.ParentId is not null) || !stored.Exists(c => Fallbacks.ContainsKey(c.Id.Value)))
		{
			return Result.Success(false);
		}

		var placed = Place(stored, country);
		if (placed.IsFailure)
		{
			return Result.Failure<bool>(placed.Error, placed.ResultExceptionType);
		}

		var merged = placed.Value.Where(c => Fallbacks.ContainsKey(c.Id.Value)).ToList();
		var remaining = placed.Value.Where(c => !Fallbacks.ContainsKey(c.Id.Value)).ToList();
		var kept = AddOwnerKeywords(merged, remaining);
		if (kept.IsFailure)
		{
			return Result.Failure<bool>(kept.Error, kept.ResultExceptionType);
		}

		var transactions = await dbContext.Transactions.ToListAsync(cancellationToken).ConfigureAwait(false);
		Rehome(user, merged.Select(c => c.Id.Value).ToHashSet(StringComparer.Ordinal), remaining, transactions);
		dbContext.Categories.RemoveRange(merged);

		var saved = await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return saved.IsSuccess ? Result.Success(true) : Result.Failure<bool>(saved.Error, saved.ResultExceptionType);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Adds the missing tree nodes and attaches the stored categories that stay to their place in the tree.
	/// </summary>
	/// <returns>Every category after the change, the merged flat defaults included.</returns>
	private Result<List<Category>> Place(List<Category> stored, string country)
	{
		var byId = stored.ToDictionary(c => c.Id.Value, StringComparer.Ordinal);
		var all = new List<Category>(stored);
		foreach (var target in CategoryDefaults.CreateDefaults(country))
		{
			if (!byId.TryGetValue(target.Id.Value, out var existing))
			{
				dbContext.Categories.Add(target);
				all.Add(target);
				continue;
			}

			var attached = Attach(existing, target);
			if (attached.IsFailure)
			{
				return Result.Failure<List<Category>>(attached.Error, attached.ResultExceptionType);
			}
		}

		return Result.Success(all);
	}

	/// <summary>
	/// Places a stored category in the tree; it takes the new name, colour and keywords only when it is still the old
	/// default.
	/// </summary>
	private static Result Attach(Category existing, Category target)
	{
		var untouched = LegacyFlatCategories.ById.TryGetValue(existing.Id.Value, out var old) && old.Matches(existing);
		var changed = untouched
			? existing.Update(target.Name, target.Type, target.Color, target.Keywords, target.ParentId)
			: existing.Update(existing.Name, target.Type, existing.Color, existing.Keywords, target.ParentId);
		existing.SetCoicop(target.Coicop);
		return changed;
	}

	/// <summary>
	/// Adds the keywords of a merged category the owner edited to the node that takes over from it, without
	/// duplicates, so the owner's own words keep working.
	/// </summary>
	private static Result AddOwnerKeywords(List<Category> merged, List<Category> remaining)
	{
		foreach (var category in merged.Where(c => !LegacyFlatCategories.ById[c.Id.Value].Matches(c)))
		{
			var heir = remaining.Single(c => string.Equals(c.Id.Value, Fallbacks[category.Id.Value], StringComparison.Ordinal));
			var keywords = heir.Keywords.Union(category.Keywords, StringComparer.OrdinalIgnoreCase).ToList();
			var changed = heir.Update(heir.Name, heir.Type, heir.Color, keywords, heir.ParentId);
			if (changed.IsFailure)
			{
				return changed;
			}
		}

		return Result.Success();
	}

	/// <summary>
	/// Moves every payee rule, payment choice, split line and fixed payment that points to a merged category to the category
	/// whose keyword matches its payee, or to the merged category's fallback node.
	/// </summary>
	private static void Rehome(UserData user, HashSet<string> mergedIds, List<Category> remaining, List<Txn> transactions)
	{
		var analysis = new Analysis(transactions, remaining, user);
		var matcher = new CategoryKeywordMatcher(remaining, analysis.Tree);
		var itemsById = analysis.Items.ToDictionary(i => i.T.Id);
		var payeeNames = analysis.Items.GroupBy(i => i.SeriesKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);

		string Target(string oldId, IEnumerable<string> texts) =>
			texts.Select(t => matcher.Find(t, CategoryType.Expense)).FirstOrDefault(id => id is not null) ?? Fallbacks[oldId];

		foreach (var (key, assignment) in user.Payees.Where(kv => kv.Value.CategoryId is { } id && mergedIds.Contains(id.Value)).ToList())
		{
			var texts = new[] { payeeNames.GetValueOrDefault(key) ?? "", KeyText(key) };
			user.ReassignPayee(key, new CategoryId(Target(assignment.CategoryId!.Value, texts)), confirmed: false);
		}

		foreach (var (txnId, categoryId) in user.TxnCategories.Where(kv => mergedIds.Contains(kv.Value.Value)).ToList())
		{
			var texts = new[] { itemsById.TryGetValue(txnId, out var item) ? item.Name : "" };
			user.AssignTransactionCategory(txnId, new CategoryId(Target(categoryId.Value, texts)));
		}

		foreach (var (txnId, split) in user.Splits.ToList())
		{
			var texts = new[] { itemsById.TryGetValue(txnId, out var item) ? item.Name : "" };
			foreach (var oldId in split.Lines.Select(l => l.CategoryId).Where(id => mergedIds.Contains(id.Value)).Distinct().ToList())
			{
				user.ReassignSplitLines(txnId, oldId, new CategoryId(Target(oldId.Value, texts)));
			}
		}

		foreach (var payment in user.Recurring.Where(p => p.CategoryId is { } id && mergedIds.Contains(id.Value)).ToList())
		{
			var texts = payment.SeriesKeys.Select(KeyText).Prepend(payment.Name);
			user.ReassignFixedPayment(payment.Id, new CategoryId(Target(payment.CategoryId!.Value, texts)));
		}
	}

	/// <summary>
	/// Gets the readable part of a payee series key: <c>CARD:NETFLIX</c> becomes <c>NETFLIX</c>.
	/// </summary>
	private static string KeyText(string seriesKey)
	{
		var colon = seriesKey.IndexOf(':', StringComparison.Ordinal);
		return colon < 0 ? seriesKey : seriesKey[(colon + 1)..];
	}
	#endregion
}
