namespace KbcTracker.Application.Features.BankConnection.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The port through which the application reads an account's transactions from the bank, already
///              mapped to <see cref="Txn"/>.
///
///              Keeps the application layer free of the Enable Banking client, which lives in the infrastructure
///              layer (ADR-0003).
/// </summary>
public interface IBankTransactionSource
{
	#region Methods
	/// <summary>
	/// Downloads an account's transactions.
	/// </summary>
	/// <param name="accountUid">The account to read.</param>
	/// <param name="from">The first booking date asked for; only a hint when <paramref name="longest"/> is set.</param>
	/// <param name="longest"><see langword="true"/> to ask for the longest history the bank allows.</param>
	/// <returns>The account's transactions.</returns>
	/// <exception cref="InvalidOperationException">The bank refused the request, for example the requested period.</exception>
	Task<IReadOnlyList<Txn>> GetTransactionsAsync(string accountUid, DateOnly? from, bool longest);
	#endregion
}
