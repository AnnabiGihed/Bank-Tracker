using KbcTracker.Application.Features.BankConnection.Consent;

namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Adapts Enable Banking consent responses to provider-neutral backend contracts.
/// </summary>
/// <param name="client">The signed provider client; only the backend registers it.</param>
public sealed class EnableBankingConsentSource(EnableBankingClient client) : IBankConsentSource
{
	#region IBankConsentSource Implementation
	/// <inheritdoc />
	public async Task<IReadOnlyList<BankDefinition>> GetBanksAsync(CancellationToken cancellationToken) =>
		(await client.GetBanksAsync(cancellationToken).ConfigureAwait(false)).Select(bank => new BankDefinition(bank.Name, bank.Country)).ToArray();

	/// <inheritdoc />
	public async Task<Uri> BeginAsync(BankDefinition bank, string state, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(bank);
		return new Uri(await client.StartAuthAsync(bank.Name, bank.Country, state, cancellationToken).ConfigureAwait(false), UriKind.Absolute);
	}

	/// <inheritdoc />
	public async Task<BankConsentSession> CompleteAsync(string code, CancellationToken cancellationToken)
	{
		var session = await client.CreateSessionAsync(code, cancellationToken).ConfigureAwait(false);
		return new BankConsentSession(session.SessionId, session.AccountUids.ToArray());
	}
	#endregion
}
