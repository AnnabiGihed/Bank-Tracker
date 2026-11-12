namespace KbcTracker.Application.Tests.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A clock whose delays complete at once while its elapsed time moves on by the delay asked for, so a
///              test can run a wait of several seconds in milliseconds and still read how long it would have taken.
/// </summary>
internal sealed class InstantDelayClock : TimeProvider
{
	#region Fields
	/// <summary>The virtual time that has passed, in ticks; it only moves when a timer is created.</summary>
	private long _elapsedTicks;
	#endregion

	#region Properties
	/// <summary>Gets the virtual time that has passed through the delays awaited on this clock.</summary>
	public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref _elapsedTicks));

	/// <inheritdoc />
	public override long TimestampFrequency => TimeSpan.TicksPerSecond;
	#endregion

	#region Overrides
	/// <inheritdoc />
	public override long GetTimestamp() => Interlocked.Read(ref _elapsedTicks);

	/// <inheritdoc />
	/// <remarks>Moves the virtual time on by <paramref name="dueTime"/> and fires the timer immediately.</remarks>
	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		Interlocked.Add(ref _elapsedTicks, dueTime.Ticks);
		return System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
	}
	#endregion
}
