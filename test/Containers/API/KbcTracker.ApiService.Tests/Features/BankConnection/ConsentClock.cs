namespace KbcTracker.ApiService.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Supplies deterministic consent-state expiry without waiting for real time.
/// </summary>
internal sealed class ConsentClock : TimeProvider
{
	#region Fields
	/// <summary>The deterministic current instant used by the state cache.</summary>
	private DateTimeOffset _now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
	#endregion

	#region Public Methods
	/// <summary>Advances the clock without sleeping.</summary>
	/// <param name="duration">The elapsed interval.</param>
	public void Advance(TimeSpan duration) => _now += duration;
	#endregion

	#region Overrides
	/// <inheritdoc />
	public override DateTimeOffset GetUtcNow() => _now;
	#endregion
}
