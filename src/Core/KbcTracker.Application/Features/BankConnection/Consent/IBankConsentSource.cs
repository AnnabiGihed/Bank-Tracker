namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Keeps the read-only banking provider and its private key outside consent use cases.
/// </summary>
public interface IBankConsentSource
{
	#region Methods
	/// <summary>Lists available banks.</summary>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The available bank identities.</returns>
	Task<IReadOnlyList<BankDefinition>> GetBanksAsync(CancellationToken cancellationToken);

	/// <summary>Starts a provider consent request.</summary>
	/// <param name="bank">The bank the owner selected.</param>
	/// <param name="state">The expiring owner-bound state.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The bank's browser destination.</returns>
	Task<Uri> BeginAsync(BankDefinition bank, string state, CancellationToken cancellationToken);

	/// <summary>Exchanges a one-use consent code for backend credentials.</summary>
	/// <param name="code">The code returned after bank authorisation.</param>
	/// <param name="cancellationToken">Cancels the provider request.</param>
	/// <returns>The backend-only credentials.</returns>
	Task<BankConsentSession> CompleteAsync(string code, CancellationToken cancellationToken);
	#endregion
}
