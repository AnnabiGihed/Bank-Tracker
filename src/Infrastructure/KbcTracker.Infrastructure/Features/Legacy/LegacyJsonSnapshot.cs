namespace KbcTracker.Infrastructure.Features.Legacy;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Carries an archive read without coupling its JSON codec to the destination database.
/// </summary>
/// <param name="Transactions">The permanent transaction history.</param>
/// <param name="Categories">The stored category definitions.</param>
/// <param name="UserData">The stored decisions.</param>
/// <param name="Session">The bank consent, or null when its optional file is absent.</param>
public sealed record LegacyJsonSnapshot(IReadOnlyList<Txn> Transactions, IReadOnlyList<Category> Categories, UserData UserData, SessionInfo? Session);
