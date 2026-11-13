using System.Data;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Application.Features.Workspace.Abstractions;
using Pivot.Framework.Domain.Repositories;
using Pivot.Framework.Infrastructure.Persistence.PostgreSQL.Locking;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Persists central UI decisions through a serialised PostgreSQL unit of work.
/// </summary>
/// <param name="db">The scoped context.</param>
/// <param name="unitOfWork">The Pivot commit boundary.</param>
public sealed class PostgreSqlWorkspacePersistence(KbcTrackerDbContext db, IUnitOfWork unitOfWork) : IWorkspacePersistence
{
	#region Static Instances
	/// <summary>The shared writer lease for category and user-decision changes.</summary>
	private static readonly long WriteLockId = PostgreSqlAdvisoryLock.ComputeLockId("KbcTracker.UserDecisions");
	#endregion

	#region IWorkspacePersistence Implementation
	/// <inheritdoc />
	public async Task<WorkspaceData> ReadAsync(CancellationToken cancellationToken)
	{
		var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
		await using (transaction.ConfigureAwait(false))
		{
			var transactions = await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenBy(t => t.Id)
				.ToListAsync(cancellationToken).ConfigureAwait(false);
			var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id)
				.ToListAsync(cancellationToken).ConfigureAwait(false);
			var user = await db.UserData.AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? UserData.Empty();
			var session = await db.BankSessions.AsNoTracking().OrderByDescending(s => s.ConnectedAt).ThenBy(s => s.Id)
				.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
			EnsureDisplayCategory(categories);
			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			return new(transactions, categories, user, session);
		}
	}

	/// <inheritdoc />
	public async Task<IAsyncDisposable?> TryAcquireWriteAsync(CancellationToken cancellationToken) =>
		await PostgreSqlAdvisoryLock.TryAcquireAsync(db, WriteLockId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<WorkspaceData?> LoadForUpdateAsync(CancellationToken cancellationToken)
	{
		var transactions = await db.Transactions.AsNoTracking().OrderByDescending(t => t.Date).ThenBy(t => t.Id)
			.ToListAsync(cancellationToken).ConfigureAwait(false);
		var categories = await db.Categories.OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
		var user = await db.UserData.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		if (user is null)
		{
			// Not imported or initialised yet: staging defaults here would make the one-time import impossible.
			return null;
		}

		if (categories.Count == 0)
		{
			categories.AddRange(DefaultCategories());
			db.Categories.AddRange(categories);
		}
		else
		{
			foreach (var missing in MissingBuiltIns(categories))
			{
				categories.Add(missing);
				db.Categories.Add(missing);
			}
		}

		return new(transactions, categories, user, null);
	}

	/// <inheritdoc />
	public void AddCategory(Category category) => db.Categories.Add(category);

	/// <inheritdoc />
	public void RemoveCategory(Category category) => db.Categories.Remove(category);

	/// <inheritdoc />
	public Task<Result> SaveAsync(CancellationToken cancellationToken) => unitOfWork.SaveChangesAsync(cancellationToken);
	#endregion

	#region Private Helpers
	/// <summary>Keeps an empty development database readable without writing during a query.</summary>
	private static void EnsureDisplayCategory(List<Category> categories)
	{
		if (categories.Count == 0)
		{
			categories.AddRange(DefaultCategories());
		}
		else
		{
			categories.AddRange(MissingBuiltIns(categories));
		}
	}

	/// <summary>Builds the built-in categories that a database stored before they existed lacks, so the analysis
	/// never falls back to <c>other</c> (spending) for a loan payout.</summary>
	private static List<Category> MissingBuiltIns(List<Category> categories)
	{
		var missing = new List<Category>();
		if (!categories.Exists(c => c.Id == CategoryId.Other))
		{
			missing.Add(OtherCategory());
		}

		if (!categories.Exists(c => c.Id == CategoryId.LoanReceived))
		{
			missing.Add(CategoryDefaults.CreateLoanReceived());
		}

		return missing;
	}

	/// <summary>Builds the fallback definition required by the domain analysis.</summary>
	private static Category OtherCategory() => Category.Rehydrate(CategoryId.Other, "Other", CategoryType.Expense, Category.DefaultColor, [], builtIn: true);

	/// <summary>Uses the default category tree for an otherwise empty workspace.</summary>
	private static IEnumerable<Category> DefaultCategories() => CategoryDefaults.CreateDefaults(CategoryDefaults.DefaultCountry);
	#endregion
}
