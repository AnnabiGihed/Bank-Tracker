namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The application's <see cref="IBankTransactionSource"/> on Enable Banking: downloads through
///              <see cref="EnableBankingClient"/> and maps each transaction with <see cref="EnableBankingMapper"/>.
/// </summary>
public sealed class EnableBankingTransactionSource : IBankTransactionSource
{
	#region Fields
	private readonly EnableBankingClient _client;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="EnableBankingTransactionSource"/>.
	/// </summary>
	/// <param name="client">The Enable Banking client.</param>
	/// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
	public EnableBankingTransactionSource(EnableBankingClient client)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
	}
	#endregion

	#region IBankTransactionSource Implementation
	/// <inheritdoc />
	/// <exception cref="InvalidDataException">A downloaded transaction could not be read. Reported apart from
	/// <see cref="InvalidOperationException"/>, which means the bank refused the request and makes the sync retry.</exception>
	public async Task<IReadOnlyList<Txn>> GetTransactionsAsync(string accountUid, DateOnly? from, bool longest)
	{
		var raw = await _client.GetTransactionsAsync(accountUid, from, longest).ConfigureAwait(false);
		try
		{
			return raw.Select(transaction => EnableBankingMapper.Map(accountUid, transaction)).ToList();
		}
		catch (Exception ex) when (ex is InvalidOperationException or FormatException or OverflowException)
		{
			throw new InvalidDataException($"A transaction of account {accountUid} could not be read: {ex.Message}", ex);
		}
	}
	#endregion
}
