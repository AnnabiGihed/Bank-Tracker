namespace KbcTracker.Application.Features.BankConnection.Consent;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Tracks short-lived owner-bound consent requests and consumes them atomically once.
/// </summary>
public interface IConsentStateStore
{
	#region Methods
	/// <summary>Creates an unpredictable state when capacity allows.</summary>
	/// <param name="owner">The trusted owner identity initiating consent.</param>
	/// <returns>The state, or null when too many requests are pending.</returns>
	string? Create(string owner);

	/// <summary>Consumes a live state only for its initiating owner.</summary>
	/// <param name="state">The state echoed by the bank.</param>
	/// <param name="owner">The trusted identity completing consent.</param>
	/// <returns>True exactly once for a matching live request.</returns>
	bool TryConsume(string state, string owner);

	/// <summary>Discards a state whose provider request failed before redirect.</summary>
	/// <param name="state">The state to discard.</param>
	void Remove(string state);
	#endregion
}
