namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The banner of a sync that failed or left transactions out: what to say and which action goes with it
///              (reconnect after a failure, sync everything again after skipped transactions).
/// </summary>
/// <param name="Kind">Which situation the banner reports.</param>
/// <param name="Heading">The bold lead of the message.</param>
/// <param name="Message">The rest of the message; it can come from the backend, so a host must encode it.</param>
public sealed record SyncNotice(SyncNoticeKind Kind, string Heading, string Message);
