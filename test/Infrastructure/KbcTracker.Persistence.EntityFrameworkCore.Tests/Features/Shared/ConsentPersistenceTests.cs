using KbcTracker.Application.Features.BankConnection.Consent;
using KbcTracker.Domain.Features.BankConnection.Repositories;
using KbcTracker.Domain.Features.BankConnection.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.BankConnection;
using Pivot.Framework.Domain.Repositories;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies on PostgreSQL what a reconnect keeps of the stored bank connection.
/// </summary>
/// <param name="fixture">The shared disposable PostgreSQL instance.</param>
public sealed class ConsentPersistenceTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Reconnect Tests
	/// <summary>
	/// Verifies a reconnect in which the bank sends no accounts keeps the previously stored accounts, whether the bank
	/// reuses the session id or sends a new one, and that accounts the bank does send replace them.
	/// </summary>
	/// <param name="sessionId">The id of the session the reconnect returns.</param>
	/// <param name="sendsAccounts">Whether the bank sends the account details.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("session-1", false)]
	[InlineData("session-2", false)]
	[InlineData("session-2", true)]
	public async Task SaveAsync_Reconnect_KeepsStoredAccountsUnlessBankSendsNew(string sessionId, bool sendsAccounts)
	{
		await AddAsync(Samples.Session(), Samples.Decisions());
		OwnAccount[] sent = [new("current", "BE00999900009999", "Renamed", "CACC")];
		var scope = Services.CreateAsyncScope();
		await using (scope)
		{
			var provider = scope.ServiceProvider;
			var persistence = new PostgreSqlBankConsentPersistence(provider.GetRequiredService<KbcTrackerDbContext>(),
				provider.GetRequiredService<IBankSessionRepository>(), provider.GetRequiredService<IUnitOfWork>(), TimeProvider.System, new BankConsentSettings(90));

			var result = await persistence.SaveAsync(new BankConsentSession(sessionId, ["current"], sendsAccounts ? sent : null), CancellationToken.None);

			result.IsSuccess.Should().BeTrue();
		}

		var sessions = await ReadAsync(db => db.BankSessions.ToListAsync());
		var stored = sessions.Single(s => string.Equals(s.Id.Value, sessionId, StringComparison.Ordinal));
		if (sendsAccounts)
		{
			stored.Accounts.Should().Equal(sent);
		}
		else
		{
			stored.Accounts.Select(a => a.Uid).Should().Equal("current", "savings");
			stored.Accounts.Single(a => string.Equals(a.Uid, "savings", StringComparison.Ordinal)).Iban.Should().Be("BE00444455556666");
		}
	}
	#endregion
}
