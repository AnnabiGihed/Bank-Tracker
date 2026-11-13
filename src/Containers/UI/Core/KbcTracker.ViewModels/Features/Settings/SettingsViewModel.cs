namespace KbcTracker.ViewModels.Features.Settings;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the Settings tab shows: the state of the bank connection (accounts, until when the bank access is
///              valid, the last sync) and the stored history (period, transaction and fixed payment counts).
/// </summary>
public sealed class SettingsViewModel
{
	#region Constructors
	/// <summary>Initialises a new <see cref="SettingsViewModel"/>.</summary>
	/// <param name="analysis">The analysis of all stored data.</param>
	/// <param name="session">The bank session, or <see langword="null"/> when not connected.</param>
	/// <param name="formats">The formats of dates.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public SettingsViewModel(Analysis analysis, SessionInfo? session, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(formats);
		IsConnected = session is not null;
		AccountCount = session?.AccountUids.Count ?? 0;
		ValidUntilText = session is null ? string.Empty : formats.LocalFullDate(session.ValidUntil);
		LastSyncText = session is null ? string.Empty : formats.LocalFullDayTime(session.LastSync);
		LastSyncFailed = session?.LastError is not null;
		StoredRangeText = analysis.Items.Count == 0
			? "—"
			: $"{formats.FullDate(analysis.Items.Min(i => i.T.Date))} → {formats.FullDate(analysis.Items.Max(i => i.T.Date))}";
		TransactionCount = analysis.Items.Count;
		FixedPaymentCount = analysis.Fixed.Count;
	}
	#endregion

	#region Properties
	/// <summary>Gets a value indicating whether a bank is connected.</summary>
	public bool IsConnected { get; }

	/// <summary>Gets the number of linked accounts.</summary>
	public int AccountCount { get; }

	/// <summary>Gets the date the bank access ends, as <c>dd/MM/yyyy</c>; the owner reconnects then.</summary>
	public string ValidUntilText { get; }

	/// <summary>Gets when the bank was last read, as <c>dd/MM/yyyy HH:mm</c>.</summary>
	public string LastSyncText { get; }

	/// <summary>Gets a value indicating whether the last sync failed.</summary>
	public bool LastSyncFailed { get; }

	/// <summary>Gets the period the stored history covers, or a dash when there is none.</summary>
	public string StoredRangeText { get; }

	/// <summary>Gets the number of stored transactions.</summary>
	public int TransactionCount { get; }

	/// <summary>Gets the number of accepted fixed payments.</summary>
	public int FixedPaymentCount { get; }

	/// <summary>Gets where the history is stored.</summary>
	public static string StorageText => "Central PostgreSQL backend";

	/// <summary>Gets the note about how much history the bank shares and who keeps the rest.</summary>
	public static string HistoryNote =>
		"KBC only shares the last ~90 days. Your backend keeps every downloaded transaction in PostgreSQL. "
		+ "Keep the backend running for automatic sync and back up its database regularly. The app does not store or sync bank data locally.";
	#endregion
}
