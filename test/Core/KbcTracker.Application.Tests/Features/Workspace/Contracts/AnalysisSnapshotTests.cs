using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Shared.Services;

namespace KbcTracker.Application.Tests.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies that a snapshot from a backend older than the debts, which sends no debt fields, still restores into an
///              analysis instead of failing.
/// </summary>
public sealed class AnalysisSnapshotTests
{
	#region Restore Tests
	/// <summary>Verifies missing debt fields restore as empty lists.</summary>
	[Fact]
	public void ToAnalysis_DebtFieldsMissing_RestoredAsEmpty()
	{
		var today = new DateOnly(2026, 11, 9);
		var snapshot = AnalysisSnapshot.From(new Analysis([], CategoryDefaults.CreateDefaults("BE"), UserData.Empty(), today), today)
			with { Debts = null!, UnplannedLoanIds = null! };

		var analysis = snapshot.ToAnalysis();

		analysis.Debts.Should().BeEmpty();
		analysis.UnplannedLoans.Should().BeEmpty();
	}
	#endregion
}
