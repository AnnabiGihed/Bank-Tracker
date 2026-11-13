namespace KbcTracker.ViewModels.Tests.Features.Overview;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="OverviewViewModel"/> and <see cref="CategoryDrillDown"/> as data: the empty states,
///              the five key figures, the last twelve months, the doughnut with its roll-up, drill-down and path, the
///              fixed payments with their status, and the biggest expenses.
/// </summary>
public sealed class OverviewViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds an October with an 8 euro rail payment (payee in Rail trips), a 2 euro payment directly in Trips,
	/// a 3 euro split payment and the owner's salary.</summary>
	private static Analysis October() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 6), -2m, "Kiosk"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 10, 7), -3m, "Water Shop"),
		TestWorkspace.Payment("t4", new DateOnly(2026, 10, 1), 2500m, "Acme Payroll", "Salary October"),
	], (user, first) =>
	{
		user.AssignPayeeCategory(TestWorkspace.SeriesKeyOf(first, "t1"), new CategoryId("trav-train"));
		user.AssignPayeeCategory(TestWorkspace.SeriesKeyOf(first, "t2"), new CategoryId("trav"));
		user.SplitPayment(new TxnId("t3"), PaymentSplit.Create(-3m, [new SplitLine(2m, new CategoryId("bills"), null), new SplitLine(1m, new CategoryId("cash"), "Change")]).Value);
	});

	/// <summary>Builds payments of the given shops every month from July to October 2026, which the analysis suggests as fixed payments.</summary>
	private static Analysis Monthly(params string[] shops) => TestWorkspace.Analyse(
		[.. shops.SelectMany((shop, shopIndex) => Enumerable.Range(0, 4).Select(m => TestWorkspace.Payment($"m{shopIndex}-{m}", new DateOnly(2026, 7 + m, 5), -30m, shop)))]);
	#endregion

	#region Empty State Tests
	/// <summary>Verifies an analysis without transactions shows only the message that tells what to do next.</summary>
	[Fact]
	public void Constructor_NoTransactions_MessageDependsOnTheConnection()
	{
		var analysis = TestWorkspace.Analyse([]);
		var session = TestWorkspace.Session(DateTimeOffset.UtcNow.AddDays(90), null);

		var disconnected = new OverviewViewModel(analysis, string.Empty, null, Formats.Belgian, TestWorkspace.Today);
		var connected = new OverviewViewModel(analysis, string.Empty, session, Formats.Belgian, TestWorkspace.Today);

		disconnected.HasTransactions.Should().BeFalse();
		disconnected.EmptyMessage.Should().Contain("Connect bank");
		connected.EmptyMessage.Should().Contain("Sync");
		connected.Kpis.Should().BeEmpty();
	}
	#endregion

	#region Key Figure Tests
	/// <summary>Verifies the five key figures, in the old order, carry the month's income, spending, fixed payments, savings and what is left.</summary>
	[Fact]
	public void Kpis_MonthFigures_OldOrderAndWording()
	{
		var analysis = October();
		var month = analysis.MonthData("2026-10");

		var overview = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today);

		month.Spent.Should().Be(13m);
		overview.Kpis.Select(k => k.Label).Should().Equal("Income", "Spent", "Fixed payments", "Saved & invested", "Left over");
		overview.Kpis.Select(k => k.Key).Should().Equal("income", "spent", "fixed", "saved", "left");
		overview.Kpis[0].Value.Should().Be(Formats.Belgian.Money(month.Income)).And.Contain(",00 €");
		overview.Kpis[1].Value.Should().Be("13,00 €");
		overview.Kpis[1].Sub.Should().Be($"fixed {Formats.Belgian.WholeMoney(month.Fixed)} · variable {Formats.Belgian.WholeMoney(month.Variable)}");
		overview.Kpis[2].Sub.Should().Be("none confirmed yet");
		overview.Kpis[4].Value.Should().Be(Formats.Belgian.Money(month.Net));
		overview.Kpis[4].Sub.Should().Be("income − spent − saved");
	}

	/// <summary>Verifies the attention notice counts the suggestions and the payees to review, and is absent when there are none.</summary>
	[Fact]
	public void Attention_SuggestionsAndReview_CountedOrAbsent()
	{
		var analysis = October();

		var overview = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today);

		if (analysis.Suggestions.Count > 0 || analysis.Review.Count > 0)
		{
			overview.Attention.Should().Be(new AttentionNotice(analysis.Suggestions.Count, analysis.Review.Count));
		}
		else
		{
			overview.Attention.Should().BeNull();
		}

		new AttentionNotice(0, 2).SuggestionText.Should().BeNull();
		new AttentionNotice(0, 2).ReviewText.Should().Be("2 payee(s) with an automatic category to confirm.");
		new AttentionNotice(3, 0).SuggestionText.Should().Be("3 possible fixed payment(s) found.");
	}
	#endregion

	#region Series Tests
	/// <summary>Verifies the chart of the last months holds twelve months, oldest first, with short labels.</summary>
	[Fact]
	public void Series_FourteenMonths_LastTwelveOldestFirst()
	{
		var payments = Enumerable.Range(0, 14).Select(i => new DateOnly(2025, 10, 1).AddMonths(i))
			.Select((d, i) => TestWorkspace.Payment($"m{i}", d, -(i + 1), "Shop One")).ToList();
		var analysis = TestWorkspace.Analyse(payments);

		var overview = new OverviewViewModel(analysis, "2026-11", null, Formats.Belgian, TestWorkspace.Today);

		overview.Series.Should().HaveCount(12);
		overview.Series[0].Month.Should().Be("2025-12");
		overview.Series[^1].Month.Should().Be("2026-11");
		overview.Series[^1].Label.Should().Be("Nov 26");
		overview.Series.Select(s => s.Month).Should().BeInAscendingOrder(StringComparer.Ordinal);
	}
	#endregion

	#region Doughnut Tests
	/// <summary>Verifies the top level rolls a sub-category's spending up into its parent and tells the parent can be drilled into.</summary>
	[Fact]
	public void DrillDown_TopLevel_ParentCarriesSumOfItselfAndChild()
	{
		var analysis = October();
		var overview = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today);

		var slices = overview.DrillDown!.Slices(null);

		var trips = slices.Single(s => string.Equals(s.Id, "trav", StringComparison.Ordinal));
		trips.Value.Should().Be(10m);
		trips.Name.Should().Be("Trips");
		trips.CanDrill.Should().BeTrue();
		slices.Select(s => s.Value).Should().BeInDescendingOrder();
		slices.Should().NotContain(s => string.Equals(s.Id, "trav-train", StringComparison.Ordinal));
	}

	/// <summary>Verifies drilling in lists the sub-categories with palette colours, the part no sub-category claims as "not
	/// specified", and the path from "All".</summary>
	[Fact]
	public void DrillDown_IntoParent_SubCategoriesNotSpecifiedAndPath()
	{
		var analysis = October();
		var drill = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today).DrillDown!;

		var slices = drill.Slices("trav");
		var path = drill.Path("trav");

		slices.Should().HaveCount(2);
		slices[0].Should().Be(new CategorySlice("trav-train", "Rail trips", "#3454d1", 8m, CanDrill: false));
		slices[1].Should().Be(new CategorySlice("trav", "Trips – not specified", "#a0a7b4", 2m, CanDrill: false));
		path.Should().Equal(new Crumb(string.Empty, "All", false), new Crumb("trav", "Trips", true));
		drill.Path(null).Should().Equal(new Crumb(string.Empty, "All", true));
	}

	/// <summary>Verifies a split payment counts each part in its own category, so the totals match the bank.</summary>
	[Fact]
	public void DrillDown_SplitPayment_PartsCountInTheirOwnCategories()
	{
		var analysis = October();
		var month = analysis.MonthData("2026-10");

		var slices = new CategoryDrillDown(analysis, month).Slices(null);

		month.ByCategory["bills"].Should().Be(2m);
		slices.Sum(s => s.Value).Should().Be(month.ByCategory.Where(kv => analysis.Tree.Root(kv.Key)?.Type == CategoryType.Expense).Sum(kv => kv.Value));
		slices.Sum(s => s.Value).Should().Be(13m);
	}
	#endregion

	#region Card Tests
	/// <summary>Verifies the biggest expenses come largest first, a split payment says how many parts it has, and nothing
	/// from other months or income is listed.</summary>
	[Fact]
	public void BiggestExpenses_LargestFirstSplitShowsParts()
	{
		var analysis = October();

		var overview = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today);

		overview.BiggestExpenses.Select(e => e.Name).Should().Equal("Rail Co", "Water Shop", "Kiosk");
		overview.BiggestExpenses[0].AmountText.Should().Be("8,00 €");
		overview.BiggestExpenses[0].TagText.Should().Be("Rail trips");
		overview.BiggestExpenses[1].IsSplit.Should().BeTrue();
		overview.BiggestExpenses[1].TagText.Should().Be("Split into 2 parts");
		overview.BiggestExpenses[0].DateText.Should().Be("05/10");
	}

	/// <summary>Verifies the fixed payments card shows status, and the figure's line says what is still to come or that all is paid.</summary>
	[Fact]
	public void FixedPayments_StatusAndOutlook_PaidOrStillToCome()
	{
		var analysis = TestWorkspace.Analyse(
		[
			TestWorkspace.Payment("r1", new DateOnly(2026, 9, 3), -940m, "Landlord", "Rent"),
			TestWorkspace.Payment("r2", new DateOnly(2026, 10, 3), -940m, "Landlord", "Rent"),
		], (user, first) => user.AcceptFixedPayment(RecurringPayment.Create(new RecurringPaymentId("rent"), "Rent", new CategoryId("bills"),
			new PayeeMatch([TestWorkspace.SeriesKeyOf(first, "r1")], []), new RecurrenceRule(940m, 3m, 30), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value));

		var october = new OverviewViewModel(analysis, "2026-10", null, Formats.Belgian, TestWorkspace.Today);
		var november = new OverviewViewModel(analysis, "2026-11", null, Formats.Belgian, TestWorkspace.Today);

		october.FixedPayments.Should().ContainSingle().Which.Status.Text.Should().Be("paid 03/10");
		october.Kpis[2].Sub.Should().Be("all paid");
		october.FixedPaidText.Should().Be("1 of 1 paid this month");
		november.FixedPayments.Should().ContainSingle().Which.Status.Tone.Should().Be(PillTone.Bad);
		november.Kpis[2].Sub.Should().Be("1 still to come · 940 €");
		november.FixedPaidText.Should().Be("0 of 1 paid this month");
	}

	/// <summary>Verifies the empty fixed payments card ends in a full stop, and points to the suggestions in the singular or plural when there are some.</summary>
	[Fact]
	public void FixedPayments_NoneConfirmed_TextPointsToSuggestionsWhenWaiting()
	{
		var none = new OverviewViewModel(October(), "2026-10", null, Formats.Belgian, TestWorkspace.Today);
		var one = new OverviewViewModel(Monthly("Gym Club"), "2026-10", null, Formats.Belgian, TestWorkspace.Today);
		var two = new OverviewViewModel(Monthly("Gym Club", "Book Club"), "2026-10", null, Formats.Belgian, TestWorkspace.Today);

		none.NoFixedPaymentsText.Should().Be("No confirmed fixed payments yet.");
		none.SuggestionsWaitingText.Should().BeNull();
		one.SuggestionsWaiting.Should().Be(1);
		one.NoFixedPaymentsText.Should().Be("No confirmed fixed payments yet —");
		one.SuggestionsWaitingText.Should().Be("1 suggestion waiting");
		two.SuggestionsWaitingText.Should().Be("2 suggestions waiting");
	}
	#endregion
}
