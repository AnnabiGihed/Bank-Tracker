using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The application's <see cref="IBankTransactionSource"/> on Enable Banking: downloads through
///              <see cref="EnableBankingClient"/> and maps each transaction with <see cref="EnableBankingMapper"/>.
///
///              A transaction that cannot be read is skipped and logged, not allowed to fail the whole download:
///              the incremental sync asks for the same 30 days again, so one bad record would otherwise block every
///              sync until it left the bank's 90-day window, and the good transactions around it would be lost.
/// </summary>
public sealed partial class EnableBankingTransactionSource : IBankTransactionSource
{
	#region Fields
	private readonly EnableBankingClient _client;
	private readonly ILogger<EnableBankingTransactionSource> _log;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="EnableBankingTransactionSource"/>.
	/// </summary>
	/// <param name="client">The Enable Banking client.</param>
	/// <param name="log">Logger for the transactions that are skipped.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public EnableBankingTransactionSource(EnableBankingClient client, ILogger<EnableBankingTransactionSource> log)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_log = log ?? throw new ArgumentNullException(nameof(log));
	}
	#endregion

	#region IBankTransactionSource Implementation
	/// <inheritdoc />
	/// <remarks>Transactions that cannot be read are left out, counted and logged as warnings.</remarks>
	public async Task<BankDownload> GetTransactionsAsync(string accountUid, DateOnly? from, bool longest)
	{
		var raw = await _client.GetTransactionsAsync(accountUid, from, longest).ConfigureAwait(false);
		var transactions = new List<Txn>(raw.Count);
		foreach (var transaction in raw)
		{
			var mapped = TryMap(accountUid, transaction);
			if (mapped is not null)
			{
				transactions.Add(mapped);
			}
		}

		return new BankDownload(transactions, raw.Count - transactions.Count);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Maps one transaction, or logs why it cannot be read and returns <see langword="null"/>.
	/// </summary>
	private Txn? TryMap(string accountUid, JsonNode transaction)
	{
		try
		{
			return EnableBankingMapper.Map(accountUid, transaction);
		}
		catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or FormatException or OverflowException)
		{
			LogSkipped(accountUid, ex);
			return null;
		}
	}

	/// <summary>
	/// Logs a transaction that was skipped because it could not be read.
	/// </summary>
	[LoggerMessage(Level = LogLevel.Warning, Message = "Skipped a transaction of account {AccountUid} that could not be read")]
	private partial void LogSkipped(string accountUid, Exception exception);
	#endregion
}
