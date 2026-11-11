using Pivot.Framework.Domain.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Copies the JSON data folder into PostgreSQL once (ADR-0004). It refuses a database that already
///              holds data, so running it again can neither duplicate transactions nor overwrite decisions; the JSON
///              folder stays untouched as a read-only archive.
/// </summary>
/// <param name="dbContext">The context the data is added to.</param>
/// <param name="unitOfWork">Saves everything in one transaction.</param>
public sealed class DataImporter(KbcTrackerDbContext dbContext, IUnitOfWork unitOfWork)
{
	#region Public Methods
	/// <summary>
	/// Imports everything in one transaction, or nothing.
	/// </summary>
	/// <param name="snapshot">The data read from the JSON folder.</param>
	/// <param name="cancellationToken">Cancels the import.</param>
	/// <returns>Success, <see cref="ImportErrors.DatabaseNotEmpty"/> as a conflict, or the unit of work's save
	/// error.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
	public async Task<Result> ImportAsync(DataSnapshot snapshot, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		if (await HoldsDataAsync(cancellationToken).ConfigureAwait(false))
		{
			return Result.Failure(ImportErrors.DatabaseNotEmpty, ResultExceptionType.Conflict);
		}

		dbContext.Transactions.AddRange(snapshot.Transactions);
		dbContext.Categories.AddRange(snapshot.Categories);
		dbContext.UserData.Add(snapshot.UserData);
		if (snapshot.Session is not null)
		{
			dbContext.BankSessions.Add(snapshot.Session);
		}

		return await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Determines whether any table already holds a row.
	/// </summary>
	private async Task<bool> HoldsDataAsync(CancellationToken cancellationToken) =>
		await dbContext.Transactions.AnyAsync(cancellationToken).ConfigureAwait(false)
		|| await dbContext.Categories.AnyAsync(cancellationToken).ConfigureAwait(false)
		|| await dbContext.UserData.AnyAsync(cancellationToken).ConfigureAwait(false)
		|| await dbContext.BankSessions.AnyAsync(cancellationToken).ConfigureAwait(false);
	#endregion
}
