using System.Text.Json.Serialization;

namespace KbcTracker.Infrastructure.Features.Legacy.Documents;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The bank connection as stored in <c>session.json</c>, in the file's existing shape.
/// </summary>
public sealed record SessionDocument
{
	#region Properties
	/// <summary>
	/// Gets the Enable Banking session id.
	/// </summary>
	public string SessionId { get; init; } = "";

	/// <summary>
	/// Gets the accounts the user gave access to.
	/// </summary>
	public IReadOnlyList<string> AccountUids { get; init; } = [];

	/// <summary>
	/// Gets when the user connected.
	/// </summary>
	public DateTimeOffset ConnectedAt { get; init; }

	/// <summary>
	/// Gets when the consent expires.
	/// </summary>
	public DateTimeOffset ValidUntil { get; init; }

	/// <summary>
	/// Gets when the last successful sync ended.
	/// </summary>
	public DateTimeOffset? LastSync { get; init; }

	/// <summary>
	/// Gets the error of the last failed sync.
	/// </summary>
	public string? LastError { get; init; }

	/// <summary>
	/// Gets how many transactions the last successful sync left out. Written only when not 0, so files without
	/// skipped transactions keep the shape older versions wrote.
	/// </summary>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public int SkippedTransactions { get; init; }
	#endregion

	#region Public Methods
	/// <summary>
	/// Stores a bank connection.
	/// </summary>
	/// <param name="session">The connection.</param>
	/// <returns>The document.</returns>
	public static SessionDocument From(SessionInfo session) => new()
	{
		SessionId = session.Id.Value,
		AccountUids = [.. session.AccountUids],
		ConnectedAt = session.ConnectedAt,
		ValidUntil = session.ValidUntil,
		LastSync = session.LastSync,
		LastError = session.LastError,
		SkippedTransactions = session.SkippedTransactions,
	};

	/// <summary>
	/// Rebuilds the stored connection, or <see langword="null"/> when it has no session id.
	/// </summary>
	/// <returns>The connection, or <see langword="null"/>.</returns>
	public SessionInfo? ToDomain() => string.IsNullOrWhiteSpace(SessionId)
		? null
		: SessionInfo.Rehydrate(new BankSessionId(SessionId), (AccountUids ?? []).Where(u => !string.IsNullOrWhiteSpace(u)), ConnectedAt, ValidUntil, LastSync, LastError, SkippedTransactions);
	#endregion
}
