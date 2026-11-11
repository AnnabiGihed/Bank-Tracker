using System.Net;
using System.Net.Http.Json;
using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Application.Features.BankConnection.Abstractions;
using KbcTracker.Application.Features.BankConnection.Queries;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies consent HTTP endpoints persist credentials in PostgreSQL and never disclose them to clients.
/// </summary>
/// <param name="fixture">The isolated PostgreSQL backend.</param>
public sealed class BankConsentTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Consent Tests
	/// <summary>Verifies bank selection and a valid callback replace consent without losing transaction history, and
	/// that the new consent is followed by a sync of the whole history the bank still holds.</summary>
	/// <returns>A task that completes when verified.</returns>
	/// <param name="sameId">Whether the bank reused the original consent identifier.</param>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Callback_ValidState_PersistsConsentAndRejectsReplay(bool sameId)
	{
		var bank = Substitute.For<IBankConsentSource>();
		var sessionId = sameId ? ApiFixture.PrivateSession : "new-private-credential";
		bank.GetBanksAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<BankDefinition>>([new("KBC", "BE")]));
		string? state = null;
		bank.BeginAsync(Arg.Any<BankDefinition>(), Arg.Do<string>(value => state = value), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new Uri("https://bank.example.invalid/consent")));
		bank.CompleteAsync("valid-code", Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new BankConsentSession(sessionId, ["new-account"])));
		var transactions = ApiFixture.DownloadingSource();
		using var factory = fixture.CreateFactory("Development", bank, transactionSource: transactions);
		using var client = factory.CreateClient();
		var lastSync = DateTimeOffset.UtcNow.AddHours(-2);
		var setupScope = factory.Services.CreateAsyncScope();
		await using (setupScope)
		{
			var db = setupScope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			var current = await db.BankSessions.OrderByDescending(session => session.ConnectedAt).FirstAsync();
			current.RecordFullSync(lastSync, 0);
			await db.SaveChangesAsync();
		}

		var banks = await client.GetFromJsonAsync<BankDefinition[]>(new Uri("/api/v1/banks", UriKind.Relative));
		banks.Should().ContainSingle().Which.Name.Should().Be("KBC");
		using var begin = await client.PostAsJsonAsync(new Uri("/api/v1/consent", UriKind.Relative), new BeginConsentRequest("KBC", "BE"));
		begin.StatusCode.Should().Be(HttpStatusCode.OK);
		state.Should().NotBeNullOrWhiteSpace();

		using var callback = await client.GetAsync(new Uri($"/api/v1/consent/callback?code=valid-code&state={state}", UriKind.Relative));
		callback.StatusCode.Should().Be(HttpStatusCode.OK);
		var body = await callback.Content.ReadAsStringAsync();
		body.Should().NotContain(sessionId).And.NotContain("new-account");
		var connection = await callback.Content.ReadFromJsonAsync<ConnectionStatus>();
		connection.Should().NotBeNull();
		connection.Connected.Should().BeTrue();
		connection.LastSync.Should().BeAfter(lastSync, "a full sync runs as soon as the consent is saved");
		await transactions.Received(1).GetTransactionsAsync("new-account", Arg.Any<DateOnly?>(), true);
		using var replay = await client.GetAsync(new Uri($"/api/v1/consent/callback?code=valid-code&state={state}", UriKind.Relative));
		replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		await bank.Received(1).CompleteAsync("valid-code", Arg.Any<CancellationToken>());
		var scope = factory.Services.CreateAsyncScope();
		await using (scope)
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			var current = await db.BankSessions.OrderByDescending(session => session.ConnectedAt).FirstAsync();
			current.Id.Value.Should().Be(sessionId);
			current.AccountUids.Should().Equal("new-account");
			(await db.Transactions.CountAsync()).Should().Be(2, "the existing history is kept and the full sync added to it");
		}
	}

	/// <summary>Verifies provider failure callbacks consume state and never echo provider error details.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Callback_ProviderDenied_ConsumesStateWithoutExposingError()
	{
		var bank = Substitute.For<IBankConsentSource>();
		string? state = null;
		bank.BeginAsync(Arg.Any<BankDefinition>(), Arg.Do<string>(value => state = value), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new Uri("https://bank.example.invalid/consent")));
		using var factory = fixture.CreateFactory("Development", bank);
		using var client = factory.CreateClient();
		using var begin = await client.PostAsJsonAsync(new Uri("/api/v1/consent", UriKind.Relative), new BeginConsentRequest("KBC", "BE"));
		begin.StatusCode.Should().Be(HttpStatusCode.OK);

		using var denied = await client.GetAsync(new Uri($"/api/v1/consent/callback?error=private-provider-error&state={state}", UriKind.Relative));
		denied.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await denied.Content.ReadAsStringAsync()).Should().NotContain("private-provider-error");
		using var replay = await client.GetAsync(new Uri($"/api/v1/consent/callback?code=anything&state={state}", UriKind.Relative));
		replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		await bank.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
	}

	/// <summary>Verifies forged state cannot exchange a bank code.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Callback_UnknownState_DoesNotCallProvider()
	{
		var bank = Substitute.For<IBankConsentSource>();
		using var factory = fixture.CreateFactory("Development", bank);
		using var client = factory.CreateClient();

		using var callback = await client.GetAsync(new Uri("/api/v1/consent/callback?code=unused&state=forged", UriKind.Relative));

		callback.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		await bank.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
	}

	/// <summary>Verifies a running sync excludes a consent exchange before new provider credentials are issued.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Callback_SyncLeaseHeld_RejectsBeforeExchangingCode()
	{
		var bank = Substitute.For<IBankConsentSource>();
		using var factory = fixture.CreateFactory("Development", bank);
		using var client = factory.CreateClient();
		var state = factory.Services.GetRequiredService<IConsentStateStore>().Create("development");
		var scope = factory.Services.CreateAsyncScope();
		await using (scope)
		{
			var lease = await scope.ServiceProvider.GetRequiredService<IBankSyncPersistence>().TryAcquireAsync(CancellationToken.None)
				?? throw new InvalidOperationException("The test must hold the sync lease.");
			await using (lease)
			{
				using var callback = await client.GetAsync(new Uri($"/api/v1/consent/callback?code=unused&state={state}", UriKind.Relative));
				callback.StatusCode.Should().Be(HttpStatusCode.Conflict);
				await bank.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
			}
		}
	}

	/// <summary>Verifies provider exceptions disclose neither private response bodies nor file paths.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Begin_ProviderFailure_ReturnsSafeProblemAndDiscardsState()
	{
		var bank = Substitute.For<IBankConsentSource>();
		string? state = null;
		bank.BeginAsync(Arg.Any<BankDefinition>(), Arg.Do<string>(value => state = value), Arg.Any<CancellationToken>())
			.Returns(Task.FromException<Uri>(new InvalidOperationException("private-key-path-and-provider-body")));
		using var factory = fixture.CreateFactory("Development", bank);
		using var client = factory.CreateClient();

		using var begin = await client.PostAsJsonAsync(new Uri("/api/v1/consent", UriKind.Relative), new BeginConsentRequest("KBC", "BE"));

		begin.StatusCode.Should().Be(HttpStatusCode.Conflict);
		(await begin.Content.ReadAsStringAsync()).Should().NotContain("private-key-path-and-provider-body");
		state.Should().NotBeNullOrWhiteSpace();
		factory.Services.GetRequiredService<IConsentStateStore>().TryConsume(state, "development").Should().BeFalse();
	}
	#endregion
}
