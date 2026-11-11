namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Everything the JSON data folder holds, already read into domain objects, for the one-time import
///              into PostgreSQL. The host reads the folder; the importer never touches it.
/// </summary>
/// <param name="Transactions">The transaction history.</param>
/// <param name="Categories">The categories.</param>
/// <param name="UserData">The user's decisions.</param>
/// <param name="Session">The bank connection, or <see langword="null"/> when there is none.</param>
public sealed record DataSnapshot(IReadOnlyList<Txn> Transactions, IReadOnlyList<Category> Categories, UserData UserData, SessionInfo? Session);
