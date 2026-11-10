namespace KbcTracker.Domain.Features.BankConnection.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The bank connection: the Enable Banking session, the accounts it gives access to, how long the
///              consent lasts and the outcome of the last sync.
///
///              A lightweight aggregate root, replaced when the user reconnects; the consent lasts 180 days.
/// </summary>
public sealed class SessionInfo : LightweightAggregateRoot<BankSessionId>
{
	#region Constants
	/// <summary>
	/// The longest error message kept, so a long bank response does not fill the page.
	/// </summary>
	private const int MaxErrorLength = 300;
	#endregion

	#region Fields
	/// <summary>
	/// The Enable Banking ids of the accounts the user gave access to.
	/// </summary>
	private readonly List<string> _accountUids = [];
	#endregion

	#region Properties
	/// <summary>
	/// Gets the Enable Banking ids of the accounts the user gave access to; every one is synced.
	/// </summary>
	public IReadOnlyList<string> AccountUids => _accountUids.AsReadOnly();

	/// <summary>
	/// Gets when the user connected.
	/// </summary>
	public DateTimeOffset ConnectedAt { get; private set; }

	/// <summary>
	/// Gets when the consent expires; the bank refuses access after it.
	/// </summary>
	public DateTimeOffset ValidUntil { get; private set; }

	/// <summary>
	/// Gets when the last successful sync ended, or <see langword="null"/> before the first one.
	/// </summary>
	public DateTimeOffset? LastSync { get; private set; }

	/// <summary>
	/// Gets the error of the last failed sync (at most 300 characters), or <see langword="null"/> when the last sync
	/// succeeded.
	/// </summary>
	public string? LastError { get; private set; }

	/// <summary>
	/// Gets the most transactions the bank sent that could not be read and were left out since the last sync over
	/// the whole history, which resets it; 0 when all were read. Shown to the user, since the bank keeps them only
	/// about 90 days.
	/// </summary>
	public int SkippedTransactions { get; private set; }
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="SessionInfo"/> from already valid values.
	/// </summary>
	private SessionInfo(BankSessionId id, IEnumerable<string> accountUids, DateTimeOffset connectedAt, DateTimeOffset validUntil, DateTimeOffset? lastSync)
		: base(id)
	{
		_accountUids.AddRange(accountUids);
		ConnectedAt = connectedAt;
		ValidUntil = validUntil;
		LastSync = lastSync;
	}
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates the connection the user just consented to.
	/// </summary>
	/// <param name="id">The Enable Banking session id.</param>
	/// <param name="accountUids">The accounts the user gave access to.</param>
	/// <param name="connectedAt">When the user connected.</param>
	/// <param name="validUntil">When the consent expires; after <paramref name="connectedAt"/>.</param>
	/// <param name="lastSync">The last sync of the previous connection, kept so the history stays continuous.</param>
	/// <returns>The connection, or <see cref="SessionErrors.ValidityInvalid"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="accountUids"/> is <see langword="null"/>.</exception>
	public static Result<SessionInfo> Connect(BankSessionId id, IEnumerable<string> accountUids, DateTimeOffset connectedAt, DateTimeOffset validUntil, DateTimeOffset? lastSync)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(accountUids);
		return validUntil > connectedAt
			? new SessionInfo(id, accountUids, connectedAt, validUntil, lastSync)
			: Result.Failure<SessionInfo>(SessionErrors.ValidityInvalid);
	}

	/// <summary>
	/// Rebuilds a stored connection without the creation checks.
	/// </summary>
	/// <param name="id">The Enable Banking session id.</param>
	/// <param name="accountUids">The stored accounts.</param>
	/// <param name="connectedAt">When the user connected.</param>
	/// <param name="validUntil">When the consent expires.</param>
	/// <param name="lastSync">When the last successful sync ended, if any.</param>
	/// <param name="lastError">The error of the last failed sync, if any.</param>
	/// <param name="skippedTransactions">The stored count of left-out transactions.</param>
	/// <returns>The connection.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="accountUids"/> is <see langword="null"/>.</exception>
	public static SessionInfo Rehydrate(BankSessionId id, IEnumerable<string> accountUids, DateTimeOffset connectedAt, DateTimeOffset validUntil, DateTimeOffset? lastSync, string? lastError, int skippedTransactions)
	{
		ArgumentNullException.ThrowIfNull(id);
		ArgumentNullException.ThrowIfNull(accountUids);
		return new SessionInfo(id, accountUids, connectedAt, validUntil, lastSync) { LastError = lastError, SkippedTransactions = skippedTransactions };
	}
	#endregion

	#region Domain Behaviours
	/// <summary>
	/// Records a successful sync over the whole history the bank keeps, and clears the previous error. It saw every
	/// transaction the bank still has, so its count of left-out transactions replaces the previous one.
	/// </summary>
	/// <param name="endedAt">When the sync ended.</param>
	/// <param name="skippedTransactions">How many transactions could not be read and were left out; not negative.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="skippedTransactions"/> is negative.</exception>
	public void RecordFullSync(DateTimeOffset endedAt, int skippedTransactions)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(skippedTransactions);
		LastSync = endedAt;
		LastError = null;
		SkippedTransactions = skippedTransactions;
	}

	/// <summary>
	/// Records a successful sync of the last days only, and clears the previous error. It cannot see transactions
	/// left out earlier, which are still missing, so the count only grows; a full sync resets it.
	/// </summary>
	/// <param name="endedAt">When the sync ended.</param>
	/// <param name="skippedTransactions">How many transactions could not be read and were left out; not negative.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="skippedTransactions"/> is negative.</exception>
	public void RecordIncrementalSync(DateTimeOffset endedAt, int skippedTransactions)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(skippedTransactions);
		LastSync = endedAt;
		LastError = null;
		SkippedTransactions = Math.Max(SkippedTransactions, skippedTransactions);
	}

	/// <summary>
	/// Records a failed sync so the user sees why; the last successful sync is kept.
	/// </summary>
	/// <param name="message">The error message; cut to 300 characters.</param>
	/// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
	public void RecordFailure(string message)
	{
		ArgumentNullException.ThrowIfNull(message);
		LastError = message.Length > MaxErrorLength ? message[..MaxErrorLength] : message;
	}
	#endregion
}
