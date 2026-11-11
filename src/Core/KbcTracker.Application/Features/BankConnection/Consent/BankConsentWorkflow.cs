using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Application.Features.Shared;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Coordinates owner-bound consent while keeping provider failures and credentials off the wire.
/// </summary>
/// <param name="source">The read-only bank provider.</param>
/// <param name="states">The expiring one-use consent requests.</param>
/// <param name="persistence">The central consent store.</param>
/// <param name="syncPersistence">The database-wide bank workflow lease.</param>
public sealed class BankConsentWorkflow(IBankConsentSource source, IConsentStateStore states,
	IBankConsentPersistence persistence, IBankSyncPersistence syncPersistence)
{
	#region Public Methods
	/// <summary>Lists banks without returning provider errors.</summary>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The catalogue, or BankConsent.Unavailable.</returns>
	public async Task<Result<IReadOnlyList<BankDefinition>>> GetBanksAsync(CancellationToken cancellationToken)
	{
		try
		{
			return Result.Success(await source.GetBanksAsync(cancellationToken).ConfigureAwait(false));
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return Result.Failure<IReadOnlyList<BankDefinition>>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
		}
	}

	/// <summary>Creates an expiring owner-bound request and obtains a bank redirect.</summary>
	/// <param name="request">The selected bank.</param>
	/// <param name="owner">The trusted owner identity.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The bank URL, or a safe validation/capacity/provider failure.</returns>
	/// <exception cref="ArgumentNullException">The bank selection is null.</exception>
	public async Task<Result<ConsentStarted>> BeginAsync(BeginConsentRequest request, string owner, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Country) || string.IsNullOrWhiteSpace(owner))
		{
			return Result.Failure<ConsentStarted>(BankConsentErrors.SelectionInvalid);
		}

		var state = states.Create(owner);
		if (state is null)
		{
			return Result.Failure<ConsentStarted>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
		}

		try
		{
			var url = await source.BeginAsync(new BankDefinition(request.Name, request.Country), state, cancellationToken).ConfigureAwait(false);
			if (!url.IsAbsoluteUri || !string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
			{
				states.Remove(state);
				return Result.Failure<ConsentStarted>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
			}

			return Result.Success(new ConsentStarted(url));
		}
		catch (OperationCanceledException)
		{
			states.Remove(state);
			throw;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			states.Remove(state);
			return Result.Failure<ConsentStarted>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
		}
	}

	/// <summary>Consumes the request before processing success or failure and commits credentials under the sync lease.</summary>
	/// <param name="code">The provider's one-use code.</param>
	/// <param name="state">The owner-bound request state.</param>
	/// <param name="error">The provider's failure marker, never echoed to the caller.</param>
	/// <param name="owner">The trusted owner identity completing consent.</param>
	/// <param name="cancellationToken">Cancels the request.</param>
	/// <returns>The safe connection status, or an invalid-state/provider/persistence failure.</returns>
	public async Task<Result<ConnectionStatus>> CompleteAsync(string? code, string? state, string? error, string owner, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(state) || !states.TryConsume(state, owner))
		{
			return Result.Failure<ConnectionStatus>(BankConsentErrors.StateInvalid);
		}

		if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
		{
			return Result.Failure<ConnectionStatus>(BankConsentErrors.Denied);
		}

		try
		{
			var lease = await syncPersistence.TryAcquireAsync(cancellationToken).ConfigureAwait(false);
			if (lease is null)
			{
				return Result.Failure<ConnectionStatus>(BankConsentErrors.Busy, ResultExceptionType.Conflict);
			}

			await using (lease.ConfigureAwait(false))
			{
				var session = await source.CompleteAsync(code, cancellationToken).ConfigureAwait(false);
				if (string.IsNullOrWhiteSpace(session.SessionId) || session.AccountUids.Count == 0 || session.AccountUids.Any(string.IsNullOrWhiteSpace))
				{
					return Result.Failure<ConnectionStatus>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
				}

				var saved = await persistence.SaveAsync(session, cancellationToken).ConfigureAwait(false);
				return saved.IsSuccess || saved.Error == BackendErrors.NotInitialized
					? saved
					: Result.Failure<ConnectionStatus>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return Result.Failure<ConnectionStatus>(BankConsentErrors.Unavailable, ResultExceptionType.Conflict);
		}
	}
	#endregion
}
