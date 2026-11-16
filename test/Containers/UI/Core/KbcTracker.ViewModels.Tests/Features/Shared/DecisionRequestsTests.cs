namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="DecisionRequests"/> builds each action with exactly the fields the old pages sent to
///              the decisions endpoint.
/// </summary>
public sealed class DecisionRequestsTests
{
	#region Request Tests
	/// <summary>Verifies the payee decisions carry the payee's key and the chosen category, and the undo carries what to restore.</summary>
	[Fact]
	public void PayeeDecisions_KeyCategoryAndRestore_OldShape()
	{
		var previous = new PayeeAssignmentSnapshot("groceries", true);

		DecisionRequests.Confirm("DD:x").Should().Be(new DecisionRequest("confirm", Key: "DD:x"));
		DecisionRequests.PayeeCategory("DD:x", "bills").Should().Be(new DecisionRequest("payeeCategory", Key: "DD:x", CategoryId: "bills"));
		DecisionRequests.TransactionOnly("DD:x", "t1", "bills", previous).Should()
			.Be(new DecisionRequest("txnOnly", Key: "DD:x", Id: "t1", CategoryId: "bills", Restore: previous));
		DecisionRequests.Rename("DD:x", "Rent").Should().Be(new DecisionRequest("rename", Key: "DD:x", Name: "Rent"));
	}

	/// <summary>Verifies the debt decisions: the debt is the id, a payment is the key, the terms travel in the debt field.</summary>
	[Fact]
	public void DebtDecisions_IdKeyAndDebt_Shape()
	{
		var debt = new DebtRequest(DebtKind.PersonalLoan, "Friend", BorrowedOn: new DateOnly(2026, 10, 5), Principal: 100m);

		DecisionRequests.AddDebt(debt).Should().Be(new DecisionRequest("addDebt", Debt: debt));
		DecisionRequests.UpdateDebt("d1", debt).Should().Be(new DecisionRequest("updateDebt", Id: "d1", Debt: debt));
		DecisionRequests.RemoveDebt("d1").Should().Be(new DecisionRequest("removeDebt", Id: "d1"));
		DecisionRequests.MatchRepayment("d1", "t1").Should().Be(new DecisionRequest("matchRepayment", Id: "d1", Key: "t1"));
		DecisionRequests.UnmatchRepayment("d1", "t1").Should().Be(new DecisionRequest("unmatchRepayment", Id: "d1", Key: "t1"));
	}

	/// <summary>Verifies confirming several payees sends their keys together.</summary>
	[Fact]
	public void ConfirmMany_Keys_SentTogether()
	{
		var request = DecisionRequests.ConfirmMany(["a", "b"]);

		request.Action.Should().Be("confirm");
		request.Key.Should().BeNull();
		request.Keys.Should().Equal("a", "b");
	}

	/// <summary>Verifies the fixed payment decisions: accept, dismiss and mark by key, stop and update by id.</summary>
	[Fact]
	public void FixedPaymentDecisions_KeyOrId_OldShape()
	{
		DecisionRequests.Accept("SIG").Should().Be(new DecisionRequest("accept", Key: "SIG"));
		DecisionRequests.Dismiss("SIG").Should().Be(new DecisionRequest("dismiss", Key: "SIG"));
		DecisionRequests.Mark("DD:x").Should().Be(new DecisionRequest("mark", Key: "DD:x"));
		DecisionRequests.RemoveFixed("fx1").Should().Be(new DecisionRequest("removeFixed", Id: "fx1"));
		DecisionRequests.UpdateFixed("fx1", "Rent", "bills", 940m, 30).Should()
			.Be(new DecisionRequest("updateFixed", Id: "fx1", Name: "Rent", CategoryId: "bills", Amount: 940m, Cadence: 30));
	}

	/// <summary>Verifies the category decisions and the split decisions.</summary>
	[Fact]
	public void CategoryAndSplitDecisions_OldShape()
	{
		DecisionRequests.SaveCategory(null, "Groceries", "Expense", "", "#123456", "ALDI").Should()
			.Be(new DecisionRequest("saveCategory", Id: null, Name: "Groceries", Type: "Expense", ParentId: "", Color: "#123456", Keywords: "ALDI"));
		DecisionRequests.DeleteCategory("groceries").Should().Be(new DecisionRequest("deleteCategory", Id: "groceries"));
		DecisionRequests.Unsplit("t1").Should().Be(new DecisionRequest("unsplit", Id: "t1"));
		var parts = new[] { new SplitPartRequest(10m, "bills", "Water"), new SplitPartRequest(2.5m, "cash") };

		var split = DecisionRequests.Split("t1", parts);

		split.Action.Should().Be("split");
		split.Id.Should().Be("t1");
		split.Parts.Should().Equal(parts);
	}
	#endregion
}
