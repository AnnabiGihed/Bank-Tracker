namespace KbcTracker.ViewModels.Tests.Features.Settings;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SettingsViewModel"/> as data: the connection status (accounts, until when the bank
///              access is valid, the last sync) and the stored history (period and counts).
/// </summary>
public sealed class SettingsViewModelTests
{
	#region Settings Tests
	/// <summary>Verifies a connected bank shows its accounts, the date the access ends, the last sync and whether it failed.</summary>
	[Fact]
	public void Connected_StatusAndHistory()
	{
		var analysis = TestWorkspace.Analyse(
		[
			TestWorkspace.Payment("t1", new DateOnly(2026, 8, 3), -5m, "Shop One"),
			TestWorkspace.Payment("t2", new DateOnly(2026, 11, 3), -6m, "Shop One"),
		]);
		var session = TestWorkspace.Session(new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 9, 8, 15, 0, TimeSpan.Zero), "failed");

		var settings = new SettingsViewModel(analysis, session, Formats.Belgian);

		settings.IsConnected.Should().BeTrue();
		settings.AccountCount.Should().Be(2);
		settings.ValidUntilText.Should().MatchRegex(@"^\d{2}/\d{2}/2027$");
		settings.LastSyncText.Should().MatchRegex(@"^\d{2}/\d{2}/2026 \d{2}:\d{2}$");
		settings.LastSyncFailed.Should().BeTrue();
		settings.StoredRangeText.Should().Be("03/08/2026 → 03/11/2026");
		settings.TransactionCount.Should().Be(2);
		settings.FixedPaymentCount.Should().Be(0);
		SettingsViewModel.StorageText.Should().Be("Central PostgreSQL backend");
		SettingsViewModel.HistoryNote.Should().Contain("~90 days");
	}

	/// <summary>Verifies without a bank connection and without history the tab shows no connection and a dash for the period.</summary>
	[Fact]
	public void NotConnected_NoHistory_DashForThePeriod()
	{
		var settings = new SettingsViewModel(TestWorkspace.Analyse([]), null, Formats.Belgian);

		settings.IsConnected.Should().BeFalse();
		settings.AccountCount.Should().Be(0);
		settings.ValidUntilText.Should().BeEmpty();
		settings.LastSyncFailed.Should().BeFalse();
		settings.StoredRangeText.Should().Be("—");
		settings.TransactionCount.Should().Be(0);
	}
	#endregion
}
