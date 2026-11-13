namespace KbcTracker.ViewModels.Tests.Features.Income;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="IncomeViewModel"/> as data: that each section lists exactly the payments behind its
///              Overview figure and adds up to it, the salary and other income groups, the focus, the last twelve months
///              and the earnings by payer; and the words of <see cref="MonthFigureNames"/>.
/// </summary>
public sealed class IncomeViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds an October with a salary, other income, a refund and a loan payout, and a September salary.</summary>
	private static Analysis October() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 1), 2500m, "Acme Payroll", "Salary October"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 10, 9), 100m, "Aunt Ann"),
		TestWorkspace.Payment("t4", new DateOnly(2026, 10, 7), 30m, "Shop One"),
		TestWorkspace.Payment("t5", new DateOnly(2026, 10, 8), 5000m, "Kbc Bank"),
		TestWorkspace.Payment("t6", new DateOnly(2026, 9, 1), 2400m, "Acme Payroll", "Salary September"),
	], (user, _) =>
	{
		user.AssignTransactionCategory(new TxnId("t2"), new CategoryId("income-salary"));
		user.AssignTransactionCategory(new TxnId("t3"), new CategoryId("income-other"));
		user.AssignTransactionCategory(new TxnId("t4"), new CategoryId("refunds"));
		user.AssignTransactionCategory(new TxnId("t5"), CategoryId.LoanReceived);
		user.AssignTransactionCategory(new TxnId("t6"), new CategoryId("income-salary"));
	});
	#endregion

	#region Section Tests
	/// <summary>Verifies each section's payments and total equal the Overview figure of the month.</summary>
	[Fact]
	public void Sections_Month_AddUpToTheOverviewFigures()
	{
		var analysis = October();
		var month = analysis.MonthData("2026-10");

		var income = new IncomeViewModel(analysis, "2026-10", Formats.Belgian);

		income.Sections.Select(s => s.Id).Should().Equal("earnings", "refunds", "loan-received");
		income.Sections.Select(s => s.Total).Should().Equal(month.Income, month.Refunds, month.LoanReceived);
		income.Sections[0].Groups.Select(g => (g.Title, g.Rows.Select(r => r.Id))).Should().BeEquivalentTo(
			[("Salary", new[] { "t2" }), ("Other income", new[] { "t3" })]);
		income.Sections[0].Groups[0].TotalText.Should().Be(Formats.Belgian.Money(month.Salary));
		income.Sections[1].Groups.Single().Rows.Select(r => r.Id).Should().Equal("t4");
		income.Sections[2].Groups.Single().Rows.Select(r => r.Id).Should().Equal("t5");
		income.MonthLabel.Should().Be("October 2026");
	}

	/// <summary>Verifies a month without money in has empty sections that say so.</summary>
	[Fact]
	public void Sections_NoMoneyIn_EmptyWithMessage()
	{
		var income = new IncomeViewModel(October(), "2026-07", Formats.Belgian);

		income.Sections.Should().OnlyContain(s => s.Groups.Count == 0 && s.Total == 0m && s.EmptyText.Length > 0);
	}

	/// <summary>Verifies a row carries the requests of its confirm and category buttons.</summary>
	[Fact]
	public void Row_Payment_BuildsTheDecisionRequests()
	{
		var row = new IncomeViewModel(October(), "2026-10", Formats.Belgian).Sections[0].Groups[0].Rows.Single();

		row.Payer.Should().Be("Acme Payroll");
		row.AmountText.Should().Be(Formats.Belgian.Money(2500m));
		row.CategoryName.Should().Be("Salary");
		row.ConfirmRequest.Action.Should().Be("confirm");
		row.ChangeCategoryRequest("income-other").Action.Should().Be("payeeCategory");
	}

	/// <summary>Verifies the "only this one" undo gives the one payment the category and restores the payer's earlier assignment.</summary>
	[Fact]
	public void Row_OnlyThisOne_BuildsTheTxnOnlyRequest()
	{
		var row = new IncomeViewModel(October(), "2026-10", Formats.Belgian).Sections[0].Groups[0].Rows.Single();
		var previous = new PayeeAssignmentSnapshot("income", false);

		row.OnlyThisOneRequest("income-other", previous).Should().Be(new DecisionRequest("txnOnly", Key: row.SeriesKey, Id: row.Id, CategoryId: "income-other", Restore: previous));
	}

	/// <summary>Verifies the focus is the query word of the figure, and none without one.</summary>
	[Fact]
	public void Focus_Figure_IsItsQueryWord()
	{
		new IncomeViewModel(October(), "2026-10", Formats.Belgian, MonthFigure.Refunds).Focus.Should().Be("refunds");
		new IncomeViewModel(October(), "2026-10", Formats.Belgian).Focus.Should().BeNull();
	}
	#endregion

	#region Chart And Payer Tests
	/// <summary>Verifies the chart has the earnings of each month and the payers add up to the same earnings.</summary>
	[Fact]
	public void SeriesAndPayers_TwoMonths_AgreeWithEachOther()
	{
		var income = new IncomeViewModel(October(), "2026-10", Formats.Belgian);

		income.Series.Single(p => string.Equals(p.Month, "2026-10", StringComparison.Ordinal)).Earnings.Should().Be(2600m);
		income.Series.Single(p => string.Equals(p.Month, "2026-09", StringComparison.Ordinal)).Earnings.Should().Be(2400m);
		income.Payers.Select(p => (p.Name, p.Total)).Should().Equal(("Acme Payroll", 4900m), ("Aunt Ann", 100m));
		income.Payers[0].CountText.Should().Be("2 payment(s)");
		income.Payers.Sum(p => p.Total).Should().Be(income.Series.Sum(p => p.Earnings));
	}
	#endregion

	#region Figure Name Tests
	/// <summary>Verifies the query words round-trip and an unknown one is ignored.</summary>
	[Fact]
	public void FigureNames_Query_RoundTrips()
	{
		MonthFigureNames.Parse("bogus").Should().BeNull();
		MonthFigureNames.Parse(null).Should().BeNull();
		foreach (var figure in Enum.GetValues<MonthFigure>())
		{
			MonthFigureNames.Parse(MonthFigureNames.ToQuery(figure)).Should().Be(figure);
		}
	}

	/// <summary>Verifies that an undefined figure is refused instead of being named as loan money.</summary>
	[Fact]
	public void FigureNames_UndefinedFigure_Throws()
	{
		var undefined = (MonthFigure)99;

		var toQuery = () => MonthFigureNames.ToQuery(undefined);
		var label = () => MonthFigureNames.Label(undefined);

		toQuery.Should().Throw<ArgumentOutOfRangeException>();
		label.Should().Throw<ArgumentOutOfRangeException>();
	}
	#endregion
}
