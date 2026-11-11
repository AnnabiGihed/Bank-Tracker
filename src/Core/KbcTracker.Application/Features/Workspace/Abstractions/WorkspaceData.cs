namespace KbcTracker.Application.Features.Workspace.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Supplies the complete central history and decision aggregates for a workspace operation.
/// </summary>
/// <param name="Transactions">The permanent history.</param>
/// <param name="Categories">The category definitions.</param>
/// <param name="User">The user's decisions.</param>
/// <param name="Session">The current bank consent.</param>
public sealed record WorkspaceData(IReadOnlyList<Txn> Transactions, IList<Category> Categories, UserData User, SessionInfo? Session);
