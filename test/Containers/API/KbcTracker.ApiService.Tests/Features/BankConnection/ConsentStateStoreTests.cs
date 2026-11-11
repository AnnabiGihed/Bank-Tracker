using KbcTracker.ApiService.Features.BankConnection;

namespace KbcTracker.ApiService.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies pending consent requests expire, remain bounded and are consumed once by the initiating owner.
/// </summary>
public sealed class ConsentStateStoreTests
{
	#region State Tests
	/// <summary>Verifies the wrong owner cannot use a state or invalidate the correct owner's pending request.</summary>
	[Fact]
	public void TryConsume_WrongOwner_DoesNotConsumeCorrectOwnersState()
	{
		var states = new ConsentStateStore(TimeProvider.System);
		var state = states.Create("owner");
		state.Should().NotBeNull();

		states.TryConsume(state, "different-owner").Should().BeFalse();
		states.TryConsume(state, "owner").Should().BeTrue();
		states.TryConsume(state, "owner").Should().BeFalse();
	}

	/// <summary>Verifies concurrent callbacks cannot both consume the same state.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task TryConsume_ConcurrentCallbacks_ExactlyOneSucceeds()
	{
		var states = new ConsentStateStore(TimeProvider.System);
		var state = states.Create("owner");
		state.Should().NotBeNull();

		var consumed = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => states.TryConsume(state, "owner"))));

		consumed.Count(success => success).Should().Be(1);
	}

	/// <summary>Verifies expired requests cannot complete and release bounded cache capacity.</summary>
	[Fact]
	public void Create_ExpiredRequests_RejectsOldStatesAndReclaimsCapacity()
	{
		var clock = new ConsentClock();
		var states = new ConsentStateStore(clock);
		var pending = Enumerable.Range(0, 64).Select(_ => states.Create("owner")).ToArray();
		pending.Should().OnlyContain(state => state != null);
		states.Create("owner").Should().BeNull();
		clock.Advance(TimeSpan.FromMinutes(10));

		var expired = pending[0] ?? throw new InvalidOperationException("The first state must have been created.");
		states.TryConsume(expired, "owner").Should().BeFalse();
		states.Create("owner").Should().NotBeNull();
	}
	#endregion
}
