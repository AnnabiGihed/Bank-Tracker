namespace KbcTracker.Domain.Features.BankConnection.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The bank connection: the Enable Banking session, the accounts it gives access to, how long the
///              consent lasts and the outcome of the last sync.
///
///              Persisted between runs; the consent lasts 180 days, after which the user must reconnect.
/// </summary>
public sealed class SessionInfo
{
	#region Properties
	/// <summary>
	/// Gets or sets the Enable Banking session id returned by <c>POST /sessions</c>.
	/// </summary>
	public string SessionId { get; set; } = "";

	/// <summary>
	/// Gets or sets the Enable Banking ids of the accounts the user gave access to; every one is synced.
	/// </summary>
	public IList<string> AccountUids { get; set; } = [];

	/// <summary>
	/// Gets or sets when the user connected (UTC).
	/// </summary>
	public DateTimeOffset ConnectedAt { get; set; }

	/// <summary>
	/// Gets or sets when the consent expires (UTC); the bank refuses access after it.
	/// </summary>
	public DateTimeOffset ValidUntil { get; set; }

	/// <summary>
	/// Gets or sets when the last successful sync ended (UTC), or <see langword="null"/> before the first one.
	/// </summary>
	public DateTimeOffset? LastSync { get; set; }

	/// <summary>
	/// Gets or sets the error of the last failed sync (at most 300 characters), or <see langword="null"/> when the
	/// last sync succeeded.
	/// </summary>
	public string? LastError { get; set; }
	#endregion
}
