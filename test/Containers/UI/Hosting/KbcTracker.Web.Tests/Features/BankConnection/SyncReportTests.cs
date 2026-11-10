using KbcTracker.Web.Features.BankConnection;

namespace KbcTracker.Web.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the line the daily <c>--sync</c> run prints, which is all the scheduled task records.
/// </summary>
public sealed class SyncReportTests
{
	#region Describe Tests
	/// <summary>
	/// Verifies the line for a failed sync, a clean one, one that left transactions out, and one whose outcome
	/// could not be saved after transactions were stored.
	/// </summary>
	/// <param name="added">How many new transactions were stored.</param>
	/// <param name="error">The sync error, if any.</param>
	/// <param name="skipped">How many transactions are left out.</param>
	/// <param name="expected">The expected line.</param>
	[Theory]
	[InlineData(0, "Not connected to the bank yet.", 4, "Not connected to the bank yet.")]
	[InlineData(3, null, 0, "OK, 3 new transactions")]
	[InlineData(3, null, 2, "OK, 3 new transactions, 2 could not be read and were left out")]
	[InlineData(2, "session.json is locked", 0, "2 new transactions stored, but: session.json is locked")]
	public void Describe_Outcome_OneLine(int added, string? error, int skipped, string expected)
	{
		SyncReport.Describe(added, error, skipped).Should().Be(expected);
	}
	#endregion
}
