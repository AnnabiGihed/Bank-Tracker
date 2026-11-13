namespace KbcTracker.ViewModels.Tests.Features.FixedPayments;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="FixedPaymentsViewModel"/> as data: the status of a fixed payment in the month (paid,
///              paid with another amount, due, late, not this month), the month's totals, and the edit form with its
///              request.
/// </summary>
public sealed class FixedPaymentsViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds a rent of 940 euro paid on the 3rd in September and October (the October payment is a different amount when given).</summary>
	private static Analysis Rent(decimal october = 940m) => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("r1", new DateOnly(2026, 9, 3), -940m, "Landlord", "Rent"),
		TestWorkspace.Payment("r2", new DateOnly(2026, 10, 3), -october, "Landlord", "Rent"),
	], (user, first) => user.AcceptFixedPayment(RecurringPayment.Create(new RecurringPaymentId("rent"), "Rent", new CategoryId("bills"),
		new PayeeMatch([TestWorkspace.SeriesKeyOf(first, "r1")], []), new RecurrenceRule(940m, 3m, 30), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)).Value));
	#endregion

	#region Status Tests
	/// <summary>Verifies a payment of the usual amount in the month is paid, with the date it was paid.</summary>
	[Fact]
	public void Rows_PaidInMonth_PaidPillAndTotals()
	{
		var fixedPayments = new FixedPaymentsViewModel(Rent(), "2026-10", Formats.Belgian, TestWorkspace.Today);

		var row = fixedPayments.Rows.Should().ContainSingle().Subject;
		row.Status.Status.Should().Be(FixedStatus.Paid);
		row.Status.Text.Should().Be("paid 03/10");
		row.Status.Tone.Should().Be(PillTone.Ok);
		row.Name.Should().Be("Rent");
		row.Rhythm.Should().Be("Monthly");
		row.UsualDay.Should().Be(3);
		row.PaymentCount.Should().Be(2);
		row.LastPaid.Should().Be("03/10/26");
		row.DetailText.Should().Be("Monthly · 2 payment(s) · last 03/10/26");
		fixedPayments.Expected.Should().Be(940m);
		fixedPayments.Paid.Should().Be(940m);
		fixedPayments.StillToCome.Should().Be(0m);
		fixedPayments.FixedPaymentCount.Should().Be(1);
		fixedPayments.MonthLabel.Should().Be("October 2026");
	}

	/// <summary>Verifies a payment of another amount is flagged, with what it usually costs.</summary>
	[Fact]
	public void Rows_PaidOtherAmount_PaidChangedWithUsualAmount()
	{
		var fixedPayments = new FixedPaymentsViewModel(Rent(october: 1000m), "2026-10", Formats.Belgian, TestWorkspace.Today);

		var pill = fixedPayments.Rows.Single().Status;

		pill.Status.Should().Be(FixedStatus.PaidChanged);
		pill.Text.Should().StartWith("paid 1").And.EndWith("000,00 € (usually 940 €)");
		pill.Tooltip.Should().Be("Usually 940,00 €");
		pill.Tone.Should().Be(PillTone.Bad);
		fixedPayments.Expected.Should().Be(1000m);
	}

	/// <summary>Verifies a month without its payment is due while it is close to the expected date and late afterwards.</summary>
	[Fact]
	public void Rows_NotPaidYet_DueThenLate()
	{
		var analysis = Rent();

		var due = new FixedPaymentsViewModel(analysis, "2026-11", Formats.Belgian, new DateOnly(2026, 11, 4)).Rows.Single();
		var late = new FixedPaymentsViewModel(analysis, "2026-11", Formats.Belgian, new DateOnly(2026, 11, 9));

		due.Status.Text.Should().Be("due ~02/11");
		due.Status.Tone.Should().Be(PillTone.Due);
		late.Rows.Single().Status.Text.Should().Be("expected ~02/11, not seen");
		late.Rows.Single().Status.Tone.Should().Be(PillTone.Bad);
		late.StillToCome.Should().Be(940m);
		late.ExpectedText.Should().Be("940,00 €");
		late.StillToComeText.Should().Be("940,00 €");
		late.PaidText.Should().Be("0,00 €");
	}

	/// <summary>Verifies a payment that had not started in the month is left out of the list.</summary>
	[Fact]
	public void Rows_MonthBeforeTheFirstPayment_NotListed()
	{
		var fixedPayments = new FixedPaymentsViewModel(Rent(), "2026-08", Formats.Belgian, TestWorkspace.Today);

		fixedPayments.Rows.Should().BeEmpty();
		FixedPaymentsViewModel.EmptyMessage.Should().Contain("Accept the suggestions");
	}
	#endregion

	#region Edit Tests
	/// <summary>Verifies the edit form starts from the payment's values and builds the <c>updateFixed</c> request.</summary>
	[Fact]
	public void Edit_ChangedValues_UpdateFixedRequest()
	{
		var row = new FixedPaymentsViewModel(Rent(), "2026-10", Formats.Belgian, TestWorkspace.Today).Rows.Single();
		var edit = row.Edit;

		edit.Name = "Flat rent";
		edit.Amount = 950.5m;
		edit.Rhythm = 91;

		edit.IsValid.Should().BeTrue();
		edit.ToRequest().Should().Be(new DecisionRequest("updateFixed", Id: "rent", Name: "Flat rent", CategoryId: "bills", Amount: 950.5m, Cadence: 91));
		FixedPaymentsViewModel.StopTracking(row).Should().Be(new DecisionRequest("removeFixed", Id: "rent"));
	}

	/// <summary>Verifies the form refuses a blank name, an empty amount and a negative amount.</summary>
	/// <param name="name">The name typed.</param>
	/// <param name="amount">The amount typed, or <see langword="null"/> for an empty field.</param>
	[Theory]
	[InlineData(" ", 10)]
	[InlineData("Rent", null)]
	[InlineData("Rent", -1)]
	public void Edit_InvalidInput_RefusedWithoutARequest(string name, int? amount)
	{
		var edit = new FixedPaymentEdit("rent", "Rent", "bills", 940m, 30) { Name = name, Amount = amount };

		var act = () => edit.ToRequest();

		edit.IsValid.Should().BeFalse();
		act.Should().Throw<InvalidOperationException>();
	}

	/// <summary>Verifies the form rounds the rhythm to monthly, quarterly or yearly as the old form did.</summary>
	/// <param name="days">The stored rhythm in days.</param>
	/// <param name="expected">The rhythm the form shows.</param>
	[Theory]
	[InlineData(28, 30)]
	[InlineData(40, 30)]
	[InlineData(41, 91)]
	[InlineData(91, 91)]
	[InlineData(200, 91)]
	[InlineData(201, 365)]
	[InlineData(365, 365)]
	public void Rhythm_StoredDays_RoundedToTheOfferedOnes(int days, int expected)
	{
		new FixedPaymentEdit("x", "X", "other", 1m, days).Rhythm.Should().Be(expected);
		FixedPaymentEdit.Rhythms.Select(r => r.Days).Should().Equal(30, 91, 365);
	}
	#endregion

	#region Suggestion Tests
	/// <summary>Verifies a suggestion's accept and dismiss requests carry its signature.</summary>
	[Fact]
	public void Suggestion_Requests_CarryTheSignature()
	{
		var suggestion = new FixedSuggestion("SIG", "Gym", false, "30,00 €", "monthly", "monthly, stable", "Sport", "#fff", []);

		suggestion.AcceptRequest.Should().Be(new DecisionRequest("accept", Key: "SIG"));
		suggestion.DismissRequest.Should().Be(new DecisionRequest("dismiss", Key: "SIG"));
	}
	#endregion
}
