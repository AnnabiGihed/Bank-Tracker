namespace KbcTracker.ViewModels.Tests.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="TransactionsViewModel"/> as data: the filters (kind, category with its
///              sub-categories, search, not confirmed only), the count and total of what they leave, split payments
///              counting only the parts in the filtered category, the pickers, and the fixed-payment cell.
/// </summary>
public sealed class TransactionsViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds an October with a rail payment (payee in Rail trips), a bakery payment, the salary, and a fixed rent.</summary>
	private static Analysis October(Action<UserData, Analysis>? more = null) => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 6), -3m, "Bakery"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 10, 1), 2500m, "Acme Payroll", "Salary October"),
		TestWorkspace.Payment("t4", new DateOnly(2026, 10, 3), -940m, "Landlord", "Rent"),
		TestWorkspace.Payment("t0", new DateOnly(2026, 9, 3), -940m, "Landlord", "Rent"),
	], (user, first) =>
	{
		user.AssignPayeeCategory(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"));
		user.AcceptFixedPayment(RecurringPayment.Create(new RecurringPaymentId("rent"), "Rent", new CategoryId("bills"),
			new PayeeMatch([TestWorkspace.SeriesKeyOf(first, "t4")], []), new RecurrenceRule(940m, 3m, 30), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value);
		more?.Invoke(user, first);
	});

	/// <summary>Finds a row by transaction id.</summary>
	private static TransactionRow Row(TransactionsViewModel transactions, string id) => transactions.AllRows.Single(r => string.Equals(r.Id, id, StringComparison.Ordinal));
	#endregion

	#region Filter Tests
	/// <summary>Verifies the tab starts on spending, so income and nothing else is left out, and the rows come in the analysis's order.</summary>
	[Fact]
	public void VisibleRows_DefaultFilter_SpendingOnly()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian);

		transactions.Type.Should().Be(TransactionTypeFilter.Spending);
		transactions.AllRows.Should().HaveCount(4);
		transactions.VisibleRows.Select(r => r.Id).Should().BeEquivalentTo("t1", "t2", "t4");
		Row(transactions, "t3").Type.Should().Be(TransactionTypeFilter.Income);
		transactions.CountText.Should().Be("3 transactions");
	}

	/// <summary>Verifies filtering on a parent category includes the payments of its sub-categories: each row lists its
	/// category and the ancestors, and the filter lists the parent with its child indented.</summary>
	[Fact]
	public void CategoryFilter_Parent_IncludesChildPaymentsAndListsChildIndented()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian, categoryId: "trav");

		Row(transactions, "t1").CategoryIds.Should().Equal("trav-train", "trav");
		transactions.CategoryId.Should().Be("trav");
		transactions.Type.Should().Be(TransactionTypeFilter.Everything);
		transactions.VisibleRows.Select(r => r.Id).Should().Equal("t1");
		var labels = transactions.FilterCategories.Select(c => c.Label).ToList();
		labels.IndexOf("— Rail trips").Should().Be(labels.IndexOf("Trips") + 1);
		transactions.FilterCategories.Select(c => c.Id).Should().NotContain("cash");
	}

	/// <summary>Verifies a category without payments in the month is not applied and leaves the filter on spending.</summary>
	[Fact]
	public void CategoryFilter_CategoryWithoutPayments_Ignored()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian, categoryId: "cash");

		transactions.CategoryId.Should().BeNull();
		transactions.Type.Should().Be(TransactionTypeFilter.Spending);
	}

	/// <summary>Verifies the search looks in the name, the description and the amount, ignoring case, and widens the kind to everything.</summary>
	/// <param name="search">The text typed.</param>
	/// <param name="expectedId">The only payment that matches.</param>
	[Theory]
	[InlineData("rail co", "t1")]
	[InlineData("  BAKERY ", "t2")]
	[InlineData("salary", "t3")]
	[InlineData("8,00", "t1")]
	public void Search_NameDescriptionOrAmount_FindsThePayment(string search, string expectedId)
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian, search: search);

		transactions.Type.Should().Be(TransactionTypeFilter.Everything);
		transactions.VisibleRows.Select(r => r.Id).Should().Equal(expectedId);
	}

	/// <summary>Verifies the kind filter separates income and spending, and the unconfirmed filter keeps only automatic categories.</summary>
	[Fact]
	public void TypeAndUnconfirmedFilters_NarrowTheRows()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian) { Type = TransactionTypeFilter.Income };
		var incomeIds = transactions.VisibleRows.Select(r => r.Id).ToList();
		transactions.Type = TransactionTypeFilter.Everything;
		transactions.UnconfirmedOnly = true;

		incomeIds.Should().Equal("t3");
		transactions.VisibleRows.Should().OnlyContain(r => !r.Confirmed);
		Row(transactions, "t1").Confirmed.Should().BeTrue();
		transactions.VisibleRows.Select(r => r.Id).Should().NotContain("t1");
	}

	/// <summary>Verifies the total adds up money out as a positive amount and says "in" when money in outweighs it.</summary>
	[Fact]
	public void Total_OutAndIn_WordedByDirection()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian);
		var spending = (transactions.Total, transactions.TotalText);
		transactions.Type = TransactionTypeFilter.Income;

		spending.Total.Should().Be(951m);
		spending.TotalText.Should().Be("Total out: 951,00 €");
		transactions.Total.Should().Be(-2500m);
		transactions.TotalText.Should().StartWith("Total in: ").And.EndWith("500,00 €");
	}
	#endregion

	#region Split Tests
	/// <summary>Verifies a split payment shows its parts and, when filtered on a category, counts only the parts in that
	/// category (and in its ancestors), not the whole payment.</summary>
	[Fact]
	public void SplitPayment_FilteredOnCategory_TotalsOnlyItsParts()
	{
		var analysis = October((user, _) => user.SplitPayment(new TxnId("t1"),
			PaymentSplit.Create(-8m, [new SplitLine(5m, new CategoryId("trav-train"), "Ticket"), new SplitLine(3m, new CategoryId("bills"), null)]).Value));

		var onChild = new TransactionsViewModel(analysis, "2026-10", Formats.Belgian, categoryId: "trav-train");
		var onParent = new TransactionsViewModel(analysis, "2026-10", Formats.Belgian, categoryId: "trav");
		var onBills = new TransactionsViewModel(analysis, "2026-10", Formats.Belgian, categoryId: "bills");

		var row = Row(onChild, "t1");
		row.IsSplit.Should().BeTrue();
		row.Parts.Select(p => p.Text).Should().Equal("Ticket 5,00 €", analysis.CategoryById("bills").Name + " 3,00 €");
		row.Parts[0].Path.Should().Be("Trips › Rail trips");
		row.PartAmountsOut["trav"].Should().Be(5m);
		row.SplitButtonTitle.Should().Be("Edit the split");
		onChild.Total.Should().Be(5m);
		onParent.Total.Should().Be(5m);
		onBills.VisibleRows.Select(r => r.Id).Should().Contain("t1");
		onBills.Total.Should().Be(3m + 940m);
	}

	/// <summary>Verifies the split editor offers spending categories only, and only spending can be split.</summary>
	[Fact]
	public void OpenSplit_SpendingCategoriesOnly_IncomeCannotBeSplit()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian);

		var editor = transactions.OpenSplit(Row(transactions, "t1"));

		Row(transactions, "t3").CanSplit.Should().BeFalse();
		Row(transactions, "t1").CanSplit.Should().BeTrue();
		editor.SpendingCategories.Should().OnlyContain(o => o.Type == CategoryType.Expense);
		editor.TransactionId.Should().Be("t1");
	}
	#endregion

	#region Row Tests
	/// <summary>Verifies the fixed-payment cell: nothing for money in, a star for a fixed payment, a button for the rest.</summary>
	[Fact]
	public void FixedMark_IncomeFixedAndOther_NoneStarOrButton()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian);

		Row(transactions, "t3").Fixed.Should().Be(FixedMark.None);
		Row(transactions, "t4").Fixed.Should().Be(FixedMark.IsFixed);
		Row(transactions, "t4").FixedName.Should().Be("Rent");
		Row(transactions, "t2").Fixed.Should().Be(FixedMark.CanMark);
		Row(transactions, "t2").MarkFixedRequest.Action.Should().Be("mark");
	}

	/// <summary>Verifies a row shows its amount and date as before and sends the old requests: the category change for the
	/// payee, then the "only this one" undo with the assignment from before.</summary>
	[Fact]
	public void Row_AmountAndRequests_OldShape()
	{
		var transactions = new TransactionsViewModel(October(), "2026-10", Formats.Belgian);
		var row = Row(transactions, "t2");
		var previous = new PayeeAssignmentSnapshot("other", false);

		row.AmountText.Should().Be("-3,00 €");
		row.DateText.Should().Be("06/10");
		row.IsIncome.Should().BeFalse();
		Row(transactions, "t3").IsIncome.Should().BeTrue();
		row.ChangeCategoryRequest("bills").Should().Be(new DecisionRequest("payeeCategory", Key: row.SeriesKey, CategoryId: "bills"));
		row.OnlyThisOneRequest("bills", previous).Should().Be(new DecisionRequest("txnOnly", Key: row.SeriesKey, Id: "t2", CategoryId: "bills", Restore: previous));
		row.ConfirmRequest.Should().Be(new DecisionRequest("confirm", Key: row.SeriesKey));
	}

	/// <summary>Verifies the month without payments lists none and the picker groups hold every category by type.</summary>
	[Fact]
	public void EmptyMonth_NoRows_PickerStillListsEveryCategory()
	{
		var transactions = new TransactionsViewModel(October(), "2026-07", Formats.Belgian);

		transactions.AllRows.Should().BeEmpty();
		transactions.CountText.Should().Be("0 transactions");
		TransactionsViewModel.EmptyMessage.Should().Be("No transactions in this month.");
		transactions.CategoryGroups.SelectMany(g => g.Options).Select(o => o.Id).Should().Contain(["trav", "trav-train", "bills", "other"]);
	}
	#endregion
}
