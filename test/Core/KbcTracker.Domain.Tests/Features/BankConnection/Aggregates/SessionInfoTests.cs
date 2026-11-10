namespace KbcTracker.Domain.Tests.Features.BankConnection.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the bank connection held by <see cref="SessionInfo"/>: its validity and how sync outcomes
///              are recorded.
/// </summary>
public sealed class SessionInfoTests
{
	#region Test Infrastructure
	/// <summary>
	/// When the test connection was made.
	/// </summary>
	private static readonly DateTimeOffset ConnectedAt = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
	#endregion

	#region Connection And Sync Recording Tests
	/// <summary>
	/// Verifies that a consent ending before it starts is refused.
	/// </summary>
	[Fact]
	public void Connect_ValidUntilBeforeStart_Refused()
	{
		var result = SessionInfo.Connect(new BankSessionId("s"), ["acc-1"], ConnectedAt, ConnectedAt.AddDays(-1), lastSync: null);

		result.Error.Should().Be(SessionErrors.ValidityInvalid);
	}

	/// <summary>
	/// Verifies that a failure keeps the last successful sync and cuts a long message to 300 characters, and that
	/// the next success clears the error.
	/// </summary>
	[Fact]
	public void RecordFailureThenSync_ErrorCutThenCleared()
	{
		var session = SessionInfo.Connect(new BankSessionId("s"), ["acc-1"], ConnectedAt, ConnectedAt.AddDays(180), lastSync: ConnectedAt).Value;

		session.RecordFailure(new string('x', 400));
		var error = session.LastError;
		var lastSyncAfterFailure = session.LastSync;
		session.RecordFullSync(ConnectedAt.AddHours(1), skippedTransactions: 2);

		error.Should().HaveLength(300);
		lastSyncAfterFailure.Should().Be(ConnectedAt);
		session.LastError.Should().BeNull();
		session.LastSync.Should().Be(ConnectedAt.AddHours(1));
		session.SkippedTransactions.Should().Be(2);
	}

	/// <summary>
	/// Verifies that a sync of the last days keeps the count of transactions left out earlier, which it cannot see,
	/// and that only a sync of the whole history lowers it.
	/// </summary>
	[Fact]
	public void RecordIncrementalThenFullSync_CountKeptThenReplaced()
	{
		var session = SessionInfo.Connect(new BankSessionId("s"), ["acc-1"], ConnectedAt, ConnectedAt.AddDays(180), lastSync: null).Value;

		session.RecordFullSync(ConnectedAt.AddHours(1), skippedTransactions: 3);
		session.RecordIncrementalSync(ConnectedAt.AddHours(2), skippedTransactions: 1);
		var afterIncremental = session.SkippedTransactions;
		session.RecordFullSync(ConnectedAt.AddHours(3), skippedTransactions: 0);

		afterIncremental.Should().Be(3);
		session.SkippedTransactions.Should().Be(0);
	}

	/// <summary>
	/// Verifies that a negative count of left-out transactions is refused.
	/// </summary>
	[Fact]
	public void RecordFullOrIncrementalSync_NegativeSkipped_Throws()
	{
		var session = SessionInfo.Connect(new BankSessionId("s"), ["acc-1"], ConnectedAt, ConnectedAt.AddDays(180), lastSync: null).Value;

		var full = () => session.RecordFullSync(ConnectedAt, skippedTransactions: -1);
		var incremental = () => session.RecordIncrementalSync(ConnectedAt, skippedTransactions: -1);

		full.Should().Throw<ArgumentOutOfRangeException>();
		incremental.Should().Throw<ArgumentOutOfRangeException>();
	}
	#endregion
}
