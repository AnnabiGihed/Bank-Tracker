namespace KbcTracker.ViewModels.Tests.Features.Subscriptions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SubscriptionsViewModel"/> as data: that card payments repeating monthly or yearly are
///              suggested and listed as subscriptions with their cost per month and per year, that other fixed payments
///              are not, and that the Fixed payments tab leaves the subscriptions out while the Overview still counts them.
/// </summary>
public sealed class SubscriptionsViewModelTests
{
	#region Test Infrastructure
	/// <summary>The shop payments of a card subscription and a rent (not by card), each paid for three months.</summary>
	private static List<Txn> Payments() =>
	[
		TestWorkspace.Payment("s1", new DateOnly(2026, 8, 5), -13.99m, "Streamflix"),
		TestWorkspace.Payment("s2", new DateOnly(2026, 9, 5), -13.99m, "Streamflix"),
		TestWorkspace.Payment("s3", new DateOnly(2026, 10, 5), -13.99m, "Streamflix"),
		TestWorkspace.Payment("r1", new DateOnly(2026, 8, 3), -940m, "Landlord", "Rent"),
		TestWorkspace.Payment("r2", new DateOnly(2026, 9, 3), -940m, "Landlord", "Rent"),
		TestWorkspace.Payment("r3", new DateOnly(2026, 10, 3), -940m, "Landlord", "Rent"),
	];

	/// <summary>Builds the analysis with the suggestions the history gives, and with both accepted when asked.</summary>
	private static Analysis Analyse(bool accepted) => TestWorkspace.Analyse(Payments(), (user, first) =>
	{
		if (!accepted)
		{
			return;
		}

		foreach (var suggestion in first.Suggestions)
		{
			user.AcceptFixedPayment(Analysis.ToDefinition(suggestion, new RecurringPaymentId("fixed-" + suggestion.Name), new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero)).Value);
		}
	});
	#endregion

	#region Suggestion Tests
	/// <summary>Verifies the card suggestion is on the Subscriptions tab and the rent suggestion on the Fixed payments tab.</summary>
	[Fact]
	public void Suggestions_CardAndRent_SplitBetweenTheTabs()
	{
		var analysis = Analyse(accepted: false);

		var subscriptions = new SubscriptionsViewModel(analysis, "2026-10", Formats.Belgian, TestWorkspace.Today);
		var fixedPayments = new FixedPaymentsViewModel(analysis, "2026-10", Formats.Belgian, TestWorkspace.Today);

		subscriptions.Suggestions.Select(s => s.Name).Should().Equal("Streamflix");
		fixedPayments.Suggestions.Select(s => s.Name).Should().Equal("Landlord");
		subscriptions.Suggestions[0].AcceptRequest.Action.Should().Be("accept");
	}
	#endregion

	#region Row Tests
	/// <summary>Verifies the accepted subscription's cost, charges and status, and the totals, while the rent stays out.</summary>
	[Fact]
	public void Rows_Accepted_CostLastAndNextCharge()
	{
		var analysis = Analyse(accepted: true);

		var view = new SubscriptionsViewModel(analysis, "2026-10", Formats.Belgian, TestWorkspace.Today);

		var row = view.Rows.Should().ContainSingle().Subject;
		row.Name.Should().Be("Streamflix");
		row.Rhythm.Should().Be("Monthly");
		row.AmountText.Should().Be(Formats.Belgian.Money(13.99m));
		row.PerMonthText.Should().Be(Formats.Belgian.Money(13.99m));
		row.PerYearText.Should().Be(Formats.Belgian.Money(167.88m));
		row.LastChargeText.Should().Be("05/10/26");
		row.NextExpectedText.Should().Be("04/11/26");
		row.Status.Text.Should().Be("paid 05/10");
		view.PerMonth.Should().Be(13.99m);
		view.PerYear.Should().Be(167.88m);
		view.SubscriptionCount.Should().Be(1);
		SubscriptionsViewModel.FixedNote.Should().Contain("fixed payments");
	}

	/// <summary>Verifies a yearly subscription counts a twelfth per month.</summary>
	[Fact]
	public void Totals_YearlySubscription_CountsATwelfthPerMonth()
	{
		var analysis = TestWorkspace.Analyse(
		[
			TestWorkspace.Payment("y1", new DateOnly(2025, 10, 20), -120m, "Cloudbox"),
			TestWorkspace.Payment("y2", new DateOnly(2026, 10, 20), -120m, "Cloudbox"),
		], (user, first) =>
		{
			user.AcceptFixedPayment(RecurringPayment.Create(new RecurringPaymentId("cloud"), "Cloudbox", new CategoryId("bills"),
				new PayeeMatch([TestWorkspace.SeriesKeyOf(first, "y1")], []), new RecurrenceRule(120m, 3m, 365), new DateTimeOffset(2025, 10, 21, 0, 0, 0, TimeSpan.Zero)).Value);
		});

		var view = new SubscriptionsViewModel(analysis, "2026-10", Formats.Belgian, TestWorkspace.Today);

		view.Rows.Single().Rhythm.Should().Be("Yearly");
		view.PerMonth.Should().Be(10m);
		view.PerYear.Should().Be(120m);
		view.Rows.Single().NextExpectedText.Should().Be("20/10/27");
	}

	/// <summary>Verifies the Fixed payments tab leaves out the subscription and keeps the rent, and its totals are the rent's.</summary>
	[Fact]
	public void FixedPayments_WithSubscription_LeavesItOut()
	{
		var analysis = Analyse(accepted: true);

		var fixedPayments = new FixedPaymentsViewModel(analysis, "2026-10", Formats.Belgian, TestWorkspace.Today);

		fixedPayments.Rows.Select(r => r.Name).Should().Equal("Landlord");
		fixedPayments.FixedPaymentCount.Should().Be(1);
		fixedPayments.Expected.Should().Be(940m);
		analysis.MonthData("2026-10").Fixed.Should().Be(940m + 13.99m, "The Overview still counts subscriptions as fixed payments.");
	}

	/// <summary>Verifies a stop request names the recurring payment, and a missing row is refused.</summary>
	[Fact]
	public void StopTracking_Row_BuildsRemoveFixed()
	{
		var row = new SubscriptionsViewModel(Analyse(accepted: true), "2026-10", Formats.Belgian, TestWorkspace.Today).Rows.Single();

		SubscriptionsViewModel.StopTracking(row).Action.Should().Be("removeFixed");
		var act = () => SubscriptionsViewModel.StopTracking(null!);
		act.Should().Throw<ArgumentNullException>();
	}

	/// <summary>Verifies a payment of a subscription is marked so that its star opens the Subscriptions tab.</summary>
	[Fact]
	public void Transactions_SubscriptionPayment_StarPointsToSubscriptions()
	{
		var rows = new TransactionsViewModel(Analyse(accepted: true), "2026-10", Formats.Belgian).AllRows;

		rows.Single(r => string.Equals(r.Id, "s3", StringComparison.Ordinal)).FixedIsSubscription.Should().BeTrue();
		rows.Single(r => string.Equals(r.Id, "r3", StringComparison.Ordinal)).FixedIsSubscription.Should().BeFalse();
	}
	#endregion
}
