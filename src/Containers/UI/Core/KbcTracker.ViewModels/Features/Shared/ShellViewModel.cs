namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the frame around every tab shows: the sync status, the month selector, the tabs with the number
///              of open items, and the banner of a failed or partial sync. Moved from the old page kit.
/// </summary>
public sealed class ShellViewModel
{
	#region Constants
	/// <summary>The number of days left of the bank access from which a warning is shown.</summary>
	private const int AccessWarningDays = 15;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="ShellViewModel"/>.</summary>
	/// <param name="analysis">The analysis that lists the months and counts the open items.</param>
	/// <param name="session">The bank session, or <see langword="null"/> when not connected.</param>
	/// <param name="month">The selected month, as <c>yyyy-MM</c>.</param>
	/// <param name="formats">The formats of dates and months.</param>
	/// <param name="now">The current moment, to count the days the bank access has left.</param>
	/// <exception cref="ArgumentNullException"><paramref name="analysis"/>, <paramref name="month"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public ShellViewModel(Analysis analysis, SessionInfo? session, string month, Formats formats, DateTimeOffset now)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(month);
		ArgumentNullException.ThrowIfNull(formats);
		IsConnected = session is not null;
		SelectedMonth = month;
		Months = [.. analysis.AvailableMonths.Select(m => new MonthOption(m, Formats.MonthName(m)))];
		var index = analysis.AvailableMonths.IndexOf(month);
		OlderMonth = index >= 0 && index < analysis.AvailableMonths.Count - 1 ? analysis.AvailableMonths[index + 1] : null;
		NewerMonth = index > 0 ? analysis.AvailableMonths[index - 1] : null;
		Tabs =
		[
			new("overview", "Overview", 0),
			new("income", "Income", 0),
			new("fixed", "Fixed payments", analysis.Suggestions.Count(s => !s.IsSubscription)),
			new("subscriptions", "Subscriptions", analysis.Suggestions.Count(s => s.IsSubscription)),
			new("transactions", "Transactions", 0),
			new("review", "To review", analysis.Review.Count),
			new("categories", "Categories", 0),
			new("settings", "Settings", 0),
		];
		(SyncText, AccessWarning) = DescribeSync(analysis, session, formats, now);
		Notice = DescribeNotice(session);
	}
	#endregion

	#region Properties
	/// <summary>Gets a value indicating whether a bank is connected.</summary>
	public bool IsConnected { get; }

	/// <summary>Gets when the bank was last read and since when the history goes back, or "Not connected".</summary>
	public string SyncText { get; }

	/// <summary>Gets the warning that the bank access ends soon, or <see langword="null"/> when it is not close.</summary>
	public string? AccessWarning { get; }

	/// <summary>Gets the selected month, as <c>yyyy-MM</c>.</summary>
	public string SelectedMonth { get; }

	/// <summary>Gets every available month, newest first.</summary>
	public IReadOnlyList<MonthOption> Months { get; }

	/// <summary>Gets the month before the selected one, or <see langword="null"/> when it is the oldest.</summary>
	public string? OlderMonth { get; }

	/// <summary>Gets the month after the selected one, or <see langword="null"/> when it is the newest.</summary>
	public string? NewerMonth { get; }

	/// <summary>Gets the tabs, with the number of open items as a badge on those that have one.</summary>
	public IReadOnlyList<TabLink> Tabs { get; }

	/// <summary>Gets the banner of a failed or partial sync, or <see langword="null"/> for none.</summary>
	public SyncNotice? Notice { get; }
	#endregion

	#region Public Methods
	/// <summary>Tells whether a tab is about one month, and so shows the month selector.</summary>
	/// <param name="tabId">The id of the tab.</param>
	/// <returns><see langword="false"/> for Settings and To review, which are not tied to a month.</returns>
	public static bool ShowsMonthSelector(string tabId) => tabId is not ("settings" or "review");
	#endregion

	#region Private Helpers
	/// <summary>Words the sync status and the warning that the bank access ends.</summary>
	private static (string Text, string? Warning) DescribeSync(Analysis analysis, SessionInfo? session, Formats formats, DateTimeOffset now)
	{
		if (session is null)
		{
			return ("Not connected", null);
		}

		var left = (int)(session.ValidUntil - now).TotalDays;
		var text = $"Synced {formats.LocalDayTime(session.LastSync)}";
		if (analysis.Items.Count > 0)
		{
			text += $" · history since {formats.FullDate(analysis.Items.Min(i => i.T.Date))}";
		}

		return (text, left < AccessWarningDays ? $"bank access ends in {formats.Number(left)} days" : null);
	}

	/// <summary>Builds the banner of a failed sync, or, after a successful one, of the transactions the bank sent that
	/// could not be read.</summary>
	private static SyncNotice? DescribeNotice(SessionInfo? session)
	{
		if (session?.LastError is not null)
		{
			return new(SyncNoticeKind.Failed, "Last sync failed:", session.LastError);
		}

		return session is { SkippedTransactions: > 0 }
			? new(SyncNoticeKind.Skipped, "Sync:", $"{session.SkippedTransactions} transaction(s) from the bank could not be read and were left out.")
			: null;
	}
	#endregion
}
