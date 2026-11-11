using System.Security.Cryptography;
using KbcTracker.Application.Features.BankConnection.Consent;

namespace KbcTracker.ApiService.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Holds bounded, short-lived consent requests for one backend process; restart invalidates pending redirects.
/// </summary>
/// <param name="clock">The clock used consistently for creation and expiry.</param>
public sealed class ConsentStateStore(TimeProvider clock) : IConsentStateStore
{
	#region Constants
	/// <summary>The maximum simultaneous consent requests, preventing an unbounded state cache.</summary>
	private const int MaximumPendingRequests = 64;
	/// <summary>The entropy of each state, in bytes.</summary>
	private const int StateByteCount = 32;
	#endregion

	#region Static Instances
	/// <summary>The time allowed for a bank redirect to complete before the owner must start again.</summary>
	private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
	#endregion

	#region Fields
	/// <summary>Serialises creation and consumption so even concurrent callbacks consume a state only once.</summary>
	private readonly Lock _gate = new();
	/// <summary>The pending owner and expiry for each unpredictable state.</summary>
	private readonly Dictionary<string, (string Owner, DateTimeOffset Expires)> _pending = new(StringComparer.Ordinal);
	#endregion

	#region IConsentStateStore Implementation
	/// <inheritdoc />
	public string? Create(string owner)
	{
		lock (_gate)
		{
			var now = clock.GetUtcNow();
			foreach (var expired in _pending.Where(item => item.Value.Expires <= now).Select(item => item.Key).ToArray())
			{
				_pending.Remove(expired);
			}

			if (_pending.Count >= MaximumPendingRequests)
			{
				return null;
			}

			var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(StateByteCount));
			_pending.Add(state, (owner, now.Add(StateLifetime)));
			return state;
		}
	}

	/// <inheritdoc />
	public bool TryConsume(string state, string owner)
	{
		lock (_gate)
		{
			if (!_pending.TryGetValue(state, out var pending) || !string.Equals(pending.Owner, owner, StringComparison.Ordinal))
			{
				return false;
			}

			_pending.Remove(state);
			return pending.Expires > clock.GetUtcNow();
		}
	}

	/// <inheritdoc />
	public void Remove(string state)
	{
		lock (_gate)
		{
			_pending.Remove(state);
		}
	}
	#endregion
}
