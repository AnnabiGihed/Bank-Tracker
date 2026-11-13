namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Tells which situation a <see cref="SyncNotice"/> reports.
/// </summary>
public enum SyncNoticeKind
{
	/// <summary>The last sync failed; the owner reconnects the bank.</summary>
	Failed,

	/// <summary>The last sync succeeded but left transactions out; the owner syncs the whole history again.</summary>
	Skipped,
}
