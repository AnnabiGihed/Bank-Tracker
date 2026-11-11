using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Domain.Features.Shared.Services;

namespace KbcTracker.Application.Features.Workspace.Contracts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Supplies both UI clients with a central analysis and safe bank connection display state.
/// </summary>
/// <param name="Analysis">The computed view without raw bank records.</param>
/// <param name="Connection">The safe consent status.</param>
/// <param name="AccountCount">The number of linked accounts; identifiers stay on the backend.</param>
public sealed record WorkspaceSnapshot(AnalysisSnapshot Analysis, ConnectionStatus Connection, int AccountCount)
{
	#region Public Methods
	/// <summary>Restores the backend's analysis for existing page renderers.</summary>
	/// <returns>The display analysis.</returns>
	public Analysis ToAnalysis() => Analysis.ToAnalysis();

	/// <summary>Adapts safe status to the legacy page display shape, with synthetic ids only.</summary>
	/// <returns>A display-only session, or null when disconnected.</returns>
	public SessionInfo? ToDisplaySession()
	{
		if (!Connection.Connected)
		{
			return null;
		}

		var error = Connection.SyncFailed ? "The backend could not complete the last bank sync." : null;
		return SessionInfo.Rehydrate(new BankSessionId("display-only"),
			Enumerable.Range(0, AccountCount).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
			DateTimeOffset.MinValue, Connection.ValidUntil ?? DateTimeOffset.MinValue, Connection.LastSync, error, Connection.SkippedTransactions);
	}
	#endregion
}
