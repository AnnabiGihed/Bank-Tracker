using KbcTracker.Application.Features.Workspace.Abstractions;
using KbcTracker.Application.Features.Workspace.Commands;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Debts.Aggregates;
using KbcTracker.Domain.Features.Debts.Errors;
using KbcTracker.Domain.Features.Debts.ValueObjects;
using KbcTracker.Domain.Features.Splits.ValueObjects;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Tests.Features.Workspace.Commands;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies which credits a debt may track (ADR-0010): a split credit is no loan payout, and changing other
///              fields of a debt still works after its credit was recategorised, because only a changed credit is checked again.
///
///              Runs through <see cref="ApplyDecisionCommandHandler"/> over an in-memory workspace.
/// </summary>
public sealed class DebtDecisionRulesTests
{
	#region Test Infrastructure
	/// <summary>The loan payout the debts track.</summary>
	private static readonly Txn Payout = Txn.Rehydrate(new TxnId("payout"), "current", new DateOnly(2026, 10, 1), Money.Rehydrate(800m, "EUR"),
		Counterparty.Rehydrate("Lender", null), "Money in", new BankRecord("LDAS", null));

	/// <summary>The persistence double of the test.</summary>
	private readonly IWorkspacePersistence _persistence = Substitute.For<IWorkspacePersistence>();

	/// <summary>Makes the persistence hand out the workspace with the user's decisions, a free lease and successful saves.</summary>
	private ApplyDecisionCommandHandler HandlerFor(UserData user)
	{
		_persistence.LoadForUpdateAsync(Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<WorkspaceData?>(new WorkspaceData([Payout], CategoryDefaults.CreateDefaults("BE"), user, Session: null)));
		_persistence.TryAcquireWriteAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IAsyncDisposable?>(Substitute.For<IAsyncDisposable>()));
		_persistence.SaveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success()));
		return new ApplyDecisionCommandHandler(_persistence, new InstantDelayClock());
	}

	/// <summary>Builds a debt request for the payout.</summary>
	private static DecisionRequest DebtRequestFor(string action, string lender, string? id = null) =>
		new(action, Id: id, Debt: new DebtRequest(DebtKind.PersonalLoan, lender, ReceivedPaymentId: Payout.Id.Value, Plan: new RepaymentPlanRequest(800m, new DateOnly(2026, 11, 1))));
	#endregion

	#region Received Credit Tests
	/// <summary>Verifies a credit the owner split cannot be the payout of a debt.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task AddDebt_SplitCredit_Refused()
	{
		var user = UserData.Empty();
		user.SplitPayment(Payout.Id, PaymentSplit.Create(800m, [new SplitLine(300m, new CategoryId("other"), null), new SplitLine(500m, new CategoryId("bills"), null)]).Value);

		var result = await HandlerFor(user).Handle(new ApplyDecisionCommand(DebtRequestFor("addDebt", "Friend")), CancellationToken.None);

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(DebtErrors.ReceivedPaymentInvalid);
		user.Debts.Should().BeEmpty();
	}

	/// <summary>Verifies a debt can still be edited after its credit was recategorised, while a new debt on that credit is
	/// refused.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task UpdateDebt_CreditRecategorised_OtherFieldsStillEditable()
	{
		var user = UserData.Empty();
		var handler = HandlerFor(user);
		var added = await handler.Handle(new ApplyDecisionCommand(DebtRequestFor("addDebt", "Before")), CancellationToken.None);
		var id = added.Value.Id!;
		user.AssignTransactionCategory(Payout.Id, new CategoryId("other"));

		var updated = await handler.Handle(new ApplyDecisionCommand(DebtRequestFor("updateDebt", "After", id)), CancellationToken.None);
		var again = await handler.Handle(new ApplyDecisionCommand(DebtRequestFor("addDebt", "Second")), CancellationToken.None);

		updated.IsSuccess.Should().BeTrue();
		user.Debts.Single().Lender.Should().Be("After");
		again.Error.Should().Be(DebtErrors.ReceivedPaymentInvalid);
	}
	#endregion
}
