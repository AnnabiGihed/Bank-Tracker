namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A clock a test can move forward, so a token's expiry is reached by moving time instead of waiting for it.
/// </summary>
internal sealed class ShiftedClock : TimeProvider
{
	#region Fields
	/// <summary>The distance the clock is ahead of the system clock, in ticks.</summary>
	private long _offsetTicks;
	#endregion

	#region Public Methods
	/// <summary>Moves the clock forward.</summary>
	/// <param name="amount">How far to move it.</param>
	public void Advance(TimeSpan amount) => Interlocked.Add(ref _offsetTicks, amount.Ticks);
	#endregion

	#region Overrides
	/// <inheritdoc />
	public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));
	#endregion
}
