using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Workspace.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Provides consistent workspace reads and serialised atomic user-decision writes.
/// </summary>
public interface IWorkspacePersistence
{
	#region Methods
	/// <summary>Reads a consistent view without staging default rows or other changes.</summary>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The workspace aggregates.</returns>
	Task<WorkspaceData> ReadAsync(CancellationToken cancellationToken);

	/// <summary>Attempts to exclude another user-decision writer until disposal.</summary>
	/// <param name="cancellationToken">Cancels acquiring the lease.</param>
	/// <returns>The lease, or null when another writer owns it.</returns>
	Task<IAsyncDisposable?> TryAcquireWriteAsync(CancellationToken cancellationToken);

	/// <summary>Loads tracked aggregates after the write lease has been acquired.</summary>
	/// <param name="cancellationToken">Cancels the load.</param>
	/// <returns>The tracked workspace, or <see langword="null"/> when the database is not initialised yet: nothing is
	/// staged then, so the one-time import stays possible.</returns>
	Task<WorkspaceData?> LoadForUpdateAsync(CancellationToken cancellationToken);

	/// <summary>Stages a new category in the current decision unit of work.</summary>
	/// <param name="category">The definition.</param>
	void AddCategory(Category category);

	/// <summary>Stages removal of a category in the current decision unit of work.</summary>
	/// <param name="category">The definition.</param>
	void RemoveCategory(Category category);

	/// <summary>Commits the user decision and category changes together.</summary>
	/// <param name="cancellationToken">Cancels the save.</param>
	/// <returns>The unit-of-work outcome.</returns>
	Task<Result> SaveAsync(CancellationToken cancellationToken);
	#endregion
}
