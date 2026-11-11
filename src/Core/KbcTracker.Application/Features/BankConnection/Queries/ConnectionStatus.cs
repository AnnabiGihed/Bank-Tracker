namespace KbcTracker.Application.Features.BankConnection.Queries;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Returns consent and sync status without disclosing bank session ids or raw errors.
/// </summary>
/// <param name="Connected">Whether a consent has been stored.</param>
/// <param name="ValidUntil">The consent expiry, when connected.</param>
/// <param name="LastSync">The last successful sync, when any.</param>
/// <param name="SkippedTransactions">The last recorded skipped count.</param>
/// <param name="SyncFailed">Whether the last sync reported a failure.</param>
public sealed record ConnectionStatus(bool Connected, DateTimeOffset? ValidUntil, DateTimeOffset? LastSync, int SkippedTransactions, bool SyncFailed);
