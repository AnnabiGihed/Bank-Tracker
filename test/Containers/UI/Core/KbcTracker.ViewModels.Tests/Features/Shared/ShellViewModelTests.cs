namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="ShellViewModel"/>: the month selector with its neighbours, the tabs, the sync status
///              with the warning that the bank access ends, and the banner of a failed or partial sync.
/// </summary>
public sealed class ShellViewModelTests
{
	#region Test Infrastructure
	/// <summary>The moment the shell is built at.</summary>
	private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

	/// <summary>Builds an analysis with payments in three months.</summary>
	private static Analysis ThreeMonths() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 9, 3), -5m, "Shop One"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 3), -6m, "Shop One"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 11, 3), -7m, "Shop One"),
	]);
	#endregion

	#region Shell Tests
	/// <summary>Verifies the middle month has an older and a newer neighbour and the ends have one only.</summary>
	[Fact]
	public void Months_NeighboursOfTheSelectedMonth_OlderAndNewer()
	{
		var analysis = ThreeMonths();

		var middle = new ShellViewModel(analysis, null, "2026-10", Formats.Belgian, Now);
		var newest = new ShellViewModel(analysis, null, "2026-11", Formats.Belgian, Now);
		var oldest = new ShellViewModel(analysis, null, "2026-09", Formats.Belgian, Now);

		middle.OlderMonth.Should().Be("2026-09");
		middle.NewerMonth.Should().Be("2026-11");
		newest.NewerMonth.Should().BeNull();
		oldest.OlderMonth.Should().BeNull();
		middle.Months.Select(m => m.Label).Should().Equal("November 2026", "October 2026", "September 2026");
	}

	/// <summary>Verifies the tabs come in the old order, with the counts of suggestions and payees to review as badges.</summary>
	[Fact]
	public void Tabs_OldOrderWithBadges_FixedAndReviewCounts()
	{
		var analysis = ThreeMonths();

		var shell = new ShellViewModel(analysis, null, "2026-11", Formats.Belgian, Now);

		shell.Tabs.Select(t => t.Id).Should().Equal("overview", "fixed", "transactions", "review", "categories", "settings");
		shell.Tabs.Single(t => string.Equals(t.Id, "fixed", StringComparison.Ordinal)).Badge.Should().Be(analysis.Suggestions.Count);
		shell.Tabs.Single(t => string.Equals(t.Id, "review", StringComparison.Ordinal)).Badge.Should().Be(analysis.Review.Count);
		shell.Tabs.Single(t => string.Equals(t.Id, "overview", StringComparison.Ordinal)).Badge.Should().Be(0);
		ShellViewModel.ShowsMonthSelector("overview").Should().BeTrue();
		ShellViewModel.ShowsMonthSelector("settings").Should().BeFalse();
		ShellViewModel.ShowsMonthSelector("review").Should().BeFalse();
	}

	/// <summary>Verifies the status says "Not connected" without a session, and otherwise when the bank was read and
	/// since when the history goes back.</summary>
	[Fact]
	public void SyncText_ConnectedOrNot_OldWording()
	{
		var analysis = ThreeMonths();
		var session = TestWorkspace.Session(Now.AddDays(100), new DateTimeOffset(2026, 11, 9, 10, 30, 0, TimeSpan.Zero));

		var disconnected = new ShellViewModel(analysis, null, "2026-11", Formats.Belgian, Now);
		var connected = new ShellViewModel(analysis, session, "2026-11", Formats.Belgian, Now);

		disconnected.SyncText.Should().Be("Not connected");
		disconnected.IsConnected.Should().BeFalse();
		connected.IsConnected.Should().BeTrue();
		connected.SyncText.Should().StartWith("Synced ").And.EndWith(" · history since 03/09/2026");
		connected.AccessWarning.Should().BeNull();
	}

	/// <summary>Verifies the warning appears when fewer than 15 days of bank access are left.</summary>
	[Fact]
	public void AccessWarning_FewerThan15DaysLeft_SaysHowMany()
	{
		var analysis = ThreeMonths();

		var shell = new ShellViewModel(analysis, TestWorkspace.Session(Now.AddDays(10).AddHours(1), Now), "2026-11", Formats.Belgian, Now);
		var edge = new ShellViewModel(analysis, TestWorkspace.Session(Now.AddDays(15), Now), "2026-11", Formats.Belgian, Now);

		shell.AccessWarning.Should().Be("bank access ends in 10 days");
		edge.AccessWarning.Should().BeNull();
	}

	/// <summary>Verifies a failed sync shows its message, and a partial one shows how many transactions were left out.</summary>
	[Fact]
	public void Notice_FailedOrPartialSync_ReconnectOrSyncAll()
	{
		var analysis = ThreeMonths();

		var failed = new ShellViewModel(analysis, TestWorkspace.Session(Now.AddDays(90), Now, "The bank said no.", skipped: 2), "2026-11", Formats.Belgian, Now);
		var partial = new ShellViewModel(analysis, TestWorkspace.Session(Now.AddDays(90), Now, null, skipped: 2), "2026-11", Formats.Belgian, Now);
		var clean = new ShellViewModel(analysis, TestWorkspace.Session(Now.AddDays(90), Now), "2026-11", Formats.Belgian, Now);

		failed.Notice.Should().Be(new SyncNotice(SyncNoticeKind.Failed, "Last sync failed:", "The bank said no."));
		partial.Notice!.Kind.Should().Be(SyncNoticeKind.Skipped);
		partial.Notice.Message.Should().Be("2 transaction(s) from the bank could not be read and were left out.");
		clean.Notice.Should().BeNull();
	}
	#endregion
}
