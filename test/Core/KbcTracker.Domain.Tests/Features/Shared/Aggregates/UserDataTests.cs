namespace KbcTracker.Domain.Tests.Features.Shared.Aggregates;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the user's decisions held by <see cref="UserData"/>: fixed payments, dismissals, payee and
///              transaction categories, payee names and forgetting a deleted category.
/// </summary>
public sealed class UserDataTests
{
	#region Test Infrastructure
	/// <summary>
	/// The series key of the rent transfer used by the tests.
	/// </summary>
	private const string RentKey = "TO:BE00111122223333";

	/// <summary>
	/// An accepted rent payment in the given category.
	/// </summary>
	private static RecurringPayment Rent(CategoryId? categoryId = null) => RecurringPayment.Create(
		new RecurringPaymentId("rent"),
		"Rent",
		categoryId,
		new PayeeMatch([RentKey], []),
		new RecurrenceRule(940m, 15m, 30),
		new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero)).Value;
	#endregion

	#region Fixed Payment Tests
	/// <summary>
	/// Verifies that accepting a payment the user dismissed before makes it no longer dismissed, and that removing
	/// it dismisses it again so it is not suggested again.
	/// </summary>
	[Fact]
	public void AcceptThenRemove_FixedPayment_DismissalFollows()
	{
		var user = UserData.Empty();
		user.DismissSuggestion(RentKey);

		user.AcceptFixedPayment(Rent());
		var dismissedWhileTracked = user.DismissedSuggestions.Contains(RentKey);
		var removed = user.RemoveFixedPayment(new RecurringPaymentId("rent"));

		dismissedWhileTracked.Should().BeFalse();
		removed.IsSuccess.Should().BeTrue();
		user.Recurring.Should().BeEmpty();
		user.DismissedSuggestions.Should().Contain(RentKey);
	}

	/// <summary>
	/// Verifies that editing keeps the values left empty, and that an unknown payment is reported, not created.
	/// </summary>
	[Fact]
	public void UpdateFixedPayment_PartialAndUnknown_KeepsOthersAndReportsNotFound()
	{
		var user = UserData.Empty();
		user.AcceptFixedPayment(Rent());

		user.UpdateFixedPayment(new RecurringPaymentId("rent"), " New rent ", categoryId: null, amount: 0m, cadenceDays: 91).IsSuccess.Should().BeTrue();
		var unknown = user.UpdateFixedPayment(new RecurringPaymentId("missing"), "X", null, null, null);

		var rent = user.Recurring.Single();
		rent.Name.Should().Be("New rent");
		rent.Amount.Should().Be(940m, "A non-positive amount keeps the current one.");
		rent.CadenceDays.Should().Be(91);
		unknown.Error.Should().Be(RecurringPaymentErrors.NotFound);
	}

	/// <summary>
	/// Verifies that removing a fixed payment that does not exist is reported as not found and changes nothing.
	/// </summary>
	[Fact]
	public void RemoveFixedPayment_UnknownId_NotFound()
	{
		var user = UserData.Empty();
		user.AcceptFixedPayment(Rent());

		var removed = user.RemoveFixedPayment(new RecurringPaymentId("missing"));

		removed.Error.Should().Be(RecurringPaymentErrors.NotFound);
		removed.ResultExceptionType.Should().Be(Pivot.Framework.Domain.Shared.ResultExceptionType.NotFound);
		user.Recurring.Should().HaveCount(1);
		user.DismissedSuggestions.Should().BeEmpty();
	}

	/// <summary>
	/// Verifies that the collections the aggregate exposes cannot be changed by casting them back.
	/// </summary>
	[Fact]
	public void Collections_CastBack_NotWritable()
	{
		var user = UserData.Empty();
		user.AcceptFixedPayment(Rent());

		(user.Recurring as IList<RecurringPayment>)?.IsReadOnly.Should().BeTrue();
		(user.Payees as IDictionary<string, PayeeAssignment>)?.IsReadOnly.Should().BeTrue();
		(user.DismissedSuggestions as ICollection<string>)?.IsReadOnly.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that two matches with the same keys and words are equal, as a value object should be.
	/// </summary>
	[Fact]
	public void PayeeMatch_SameContent_Equal()
	{
		var first = new PayeeMatch([RentKey], ["RENT"]);
		var second = new PayeeMatch([RentKey], ["RENT"]);

		first.Should().Be(second);
		first.GetHashCode().Should().Be(second.GetHashCode());
		first.Should().NotBe(new PayeeMatch([RentKey], []));
	}

	/// <summary>
	/// Verifies that a fixed payment needs a name, a payee and a positive rule.
	/// </summary>
	[Fact]
	public void RecurringPaymentCreate_InvalidValues_Refused()
	{
		var id = new RecurringPaymentId("x");
		var match = new PayeeMatch([RentKey], []);
		var rule = new RecurrenceRule(10m, 15m, 30);
		var now = DateTimeOffset.UnixEpoch;

		RecurringPayment.Create(id, " ", null, match, rule, now).Error.Should().Be(RecurringPaymentErrors.NameRequired);
		RecurringPayment.Create(id, "X", null, new PayeeMatch([], []), rule, now).Error.Should().Be(RecurringPaymentErrors.MatchRequired);
		RecurringPayment.Create(id, "X", null, match, rule with { Amount = 0m }, now).Error.Should().Be(RecurringPaymentErrors.RuleInvalid);
	}
	#endregion

	#region Category Tests
	/// <summary>
	/// Verifies that "only this one" puts the payee's previous category back and sets the transaction's own.
	/// </summary>
	[Fact]
	public void RestorePayeeCategory_AfterAssign_PreviousBackAndTransactionSet()
	{
		var user = UserData.Empty();
		user.AssignPayeeCategory(RentKey, new CategoryId("housing"));
		var previous = user.Payees[RentKey];

		user.AssignPayeeCategory(RentKey, new CategoryId("car"));
		user.RestorePayeeCategory(RentKey, previous);
		user.AssignTransactionCategory(new TxnId("t1"), new CategoryId("car"));

		user.Payees[RentKey].CategoryId.Should().Be(new CategoryId("housing"));
		user.TxnCategories[new TxnId("t1")].Should().Be(new CategoryId("car"));
	}

	/// <summary>
	/// Verifies that forgetting a deleted category removes it from payees and transactions and clears it from
	/// fixed payments, leaving other categories alone.
	/// </summary>
	[Fact]
	public void ForgetCategory_UsedEverywhere_RemovedEverywhere()
	{
		var housing = new CategoryId("housing");
		var user = UserData.Empty();
		user.AcceptFixedPayment(Rent(housing));
		user.AssignPayeeCategory(RentKey, housing);
		user.AssignPayeeCategory("CARD:SHOP", new CategoryId("groceries"));
		user.AssignTransactionCategory(new TxnId("t1"), housing);

		user.ForgetCategory(housing);

		user.Payees.Keys.Should().Equal("CARD:SHOP");
		user.TxnCategories.Should().BeEmpty();
		user.Recurring.Single().CategoryId.Should().BeNull();
	}
	#endregion

	#region Payee Tests
	/// <summary>
	/// Verifies that a payee name is trimmed and a blank one refused.
	/// </summary>
	[Fact]
	public void RenamePayee_PaddedOrBlankName_TrimmedOrRefused()
	{
		var user = UserData.Empty();

		user.RenamePayee(RentKey, "  Landlord ").IsSuccess.Should().BeTrue();
		user.RenamePayee(RentKey, " ").Error.Should().Be(UserDataErrors.PayeeNameRequired);

		user.PayeeNames[RentKey].Should().Be("Landlord");
	}
	#endregion
}
