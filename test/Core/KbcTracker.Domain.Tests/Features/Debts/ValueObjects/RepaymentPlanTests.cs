namespace KbcTracker.Domain.Tests.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins how a repayment plan is created and derived (ADR-0010): validation, the count derived from the
///              instalment amount and the other way round, the last instalment, and the due dates (day of month capped at
///              the month's end, leap years, weekly, quarterly and yearly steps).
/// </summary>
public sealed class RepaymentPlanTests
{
	#region Test Infrastructure
	/// <summary>
	/// Creates instalments the test expects to be valid.
	/// </summary>
	private static RepaymentPlan Valid(decimal amount, DateOnly start, RepaymentFrequency frequency, int? day, decimal? each, int? count) =>
		RepaymentPlan.Instalments(amount, start, frequency, day, each, count).Value;
	#endregion

	#region One Payment Tests
	/// <summary>
	/// Verifies that a single payment has one scheduled payment of the whole amount on its date.
	/// </summary>
	[Fact]
	public void OnePayment_Valid_SchedulesTheWholeAmountOnItsDate()
	{
		var plan = RepaymentPlan.OnePayment(1200m, new DateOnly(2027, 3, 1)).Value;

		plan.IsOnePayment.Should().BeTrue();
		plan.Count.Should().Be(1);
		plan.Schedule().Should().Equal(new ScheduledPayment(new DateOnly(2027, 3, 1), 1200m));
	}

	/// <summary>
	/// Verifies that an amount that is zero, negative or has more than two decimals is refused.
	/// </summary>
	/// <param name="amount">The amount.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	[InlineData(10.005)]
	public void OnePayment_BadAmount_Fails(double amount)
	{
		var result = RepaymentPlan.OnePayment((decimal)amount, new DateOnly(2027, 3, 1));

		result.IsFailure.Should().BeTrue();
		result.Error.Should().Be(RepaymentPlanErrors.AmountInvalid);
	}

	/// <summary>
	/// Verifies that a date outside the supported years is refused.
	/// </summary>
	[Fact]
	public void OnePayment_DateOutOfRange_Fails()
	{
		RepaymentPlan.OnePayment(10m, new DateOnly(1999, 12, 31)).Error.Should().Be(RepaymentPlanErrors.StartInvalid);
	}
	#endregion

	#region Derivation Tests
	/// <summary>
	/// Verifies that giving the instalment amount derives the count by rounding up, and the last instalment takes what
	/// remains.
	/// </summary>
	[Fact]
	public void Instalments_ByAmount_CountRoundedUpAndLastTakesTheRest()
	{
		var plan = Valid(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, 300m, null);

		plan.Count.Should().Be(4);
		plan.InstalmentAmount.Should().Be(300m);
		plan.LastInstalmentAmount.Should().Be(100m);
		plan.Schedule().Select(p => p.Amount).Should().Equal(300m, 300m, 300m, 100m);
		plan.Schedule().Sum(p => p.Amount).Should().Be(1000m);
	}

	/// <summary>
	/// Verifies that an amount that divides evenly has a last instalment like the others.
	/// </summary>
	[Fact]
	public void Instalments_ByAmountDividingEvenly_LastEqualsTheOthers()
	{
		var plan = Valid(1200m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, 100m, null);

		plan.Count.Should().Be(12);
		plan.LastInstalmentAmount.Should().Be(100m);
	}

	/// <summary>
	/// Verifies that giving the count derives the instalment by rounding up to the cent; the last one is smaller.
	/// </summary>
	[Fact]
	public void Instalments_ByCount_InstalmentRoundedUpToTheCent()
	{
		var plan = Valid(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, null, 3);

		plan.InstalmentAmount.Should().Be(333.34m);
		plan.LastInstalmentAmount.Should().Be(333.32m);
		plan.Schedule().Sum(p => p.Amount).Should().Be(1000m);
	}

	/// <summary>
	/// Verifies that giving both or neither of the instalment amount and the count is refused.
	/// </summary>
	/// <param name="each">The instalment amount, or <see langword="null"/>.</param>
	/// <param name="count">The count, or <see langword="null"/>.</param>
	[Theory]
	[InlineData(null, null)]
	[InlineData(100, 10)]
	public void Instalments_NotExactlyOneOfAmountAndCount_Fails(int? each, int? count)
	{
		var result = RepaymentPlan.Instalments(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, each, count);

		result.Error.Should().Be(RepaymentPlanErrors.InstalmentChoice);
	}

	/// <summary>
	/// Verifies that an instalment above the total, or with too many decimals, is refused.
	/// </summary>
	/// <param name="each">The instalment amount.</param>
	[Theory]
	[InlineData(1000.01)]
	[InlineData(0)]
	[InlineData(10.001)]
	public void Instalments_BadInstalmentAmount_Fails(double each)
	{
		var result = RepaymentPlan.Instalments(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, (decimal)each, null);

		result.Error.Should().Be(RepaymentPlanErrors.InstalmentAmountInvalid);
	}

	/// <summary>
	/// Verifies that a count outside 1 to 600 is refused, also when it derives from a tiny instalment.
	/// </summary>
	[Fact]
	public void Instalments_CountOutOfRange_Fails()
	{
		RepaymentPlan.Instalments(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, null, 0).Error.Should().Be(RepaymentPlanErrors.CountInvalid);
		RepaymentPlan.Instalments(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, null, 601).Error.Should().Be(RepaymentPlanErrors.CountInvalid);
		RepaymentPlan.Instalments(1000m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, 1m, null).Error.Should().Be(RepaymentPlanErrors.CountInvalid);
		RepaymentPlan.Instalments(1200m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, null, 600).IsSuccess.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that a count that would leave nothing for the last instalment is refused.
	/// </summary>
	[Fact]
	public void Instalments_CountLeavingNothingForTheLast_Fails()
	{
		var result = RepaymentPlan.Instalments(0.05m, new DateOnly(2026, 12, 5), RepaymentFrequency.Monthly, null, null, 10);

		result.Error.Should().Be(RepaymentPlanErrors.CountInvalid);
	}

	/// <summary>
	/// Verifies the day-of-month rules: 1 to 31, and none for weekly instalments.
	/// </summary>
	[Fact]
	public void Instalments_DayOfMonth_ValidatedAndNotForWeekly()
	{
		var start = new DateOnly(2026, 12, 5);

		RepaymentPlan.Instalments(100m, start, RepaymentFrequency.Monthly, 0, 10m, null).Error.Should().Be(RepaymentPlanErrors.DayOfMonthInvalid);
		RepaymentPlan.Instalments(100m, start, RepaymentFrequency.Monthly, 32, 10m, null).Error.Should().Be(RepaymentPlanErrors.DayOfMonthInvalid);
		RepaymentPlan.Instalments(100m, start, RepaymentFrequency.Weekly, 5, 10m, null).Error.Should().Be(RepaymentPlanErrors.DayOfMonthInvalid);
		RepaymentPlan.Instalments(100m, start, RepaymentFrequency.Monthly, 31, 10m, null).IsSuccess.Should().BeTrue();
	}
	#endregion

	#region Schedule Tests
	/// <summary>
	/// Verifies that a day that some months lack falls on the last day of those months, counting from the chosen day
	/// every time (not from the previous, shortened date).
	/// </summary>
	[Fact]
	public void Schedule_Day31_CappedAtMonthEnd()
	{
		var plan = Valid(400m, new DateOnly(2027, 1, 31), RepaymentFrequency.Monthly, null, 100m, null);

		plan.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30));
	}

	/// <summary>
	/// Verifies that February has 29 days in a leap year.
	/// </summary>
	[Fact]
	public void Schedule_Day31InLeapYear_Feb29()
	{
		var plan = Valid(200m, new DateOnly(2028, 1, 31), RepaymentFrequency.Monthly, null, 100m, null);

		plan.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2028, 1, 31), new DateOnly(2028, 2, 29));
	}

	/// <summary>
	/// Verifies that a chosen day before the start date's day puts the first instalment in the next period, and one on or
	/// after it keeps the first instalment in the start month.
	/// </summary>
	[Fact]
	public void Schedule_ChosenDay_FirstInstalmentOnOrAfterStart()
	{
		var later = Valid(200m, new DateOnly(2026, 12, 15), RepaymentFrequency.Monthly, 5, 100m, null);
		var sameMonth = Valid(200m, new DateOnly(2026, 12, 15), RepaymentFrequency.Monthly, 20, 100m, null);

		later.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2027, 1, 5), new DateOnly(2027, 2, 5));
		sameMonth.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2026, 12, 20), new DateOnly(2027, 1, 20));
	}

	/// <summary>
	/// Verifies that weekly instalments fall every seven days from the start date.
	/// </summary>
	[Fact]
	public void Schedule_Weekly_EverySevenDays()
	{
		var plan = Valid(300m, new DateOnly(2026, 12, 30), RepaymentFrequency.Weekly, null, 100m, null);

		plan.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 6), new DateOnly(2027, 1, 13));
	}

	/// <summary>
	/// Verifies the quarterly and yearly steps, with 29 February kept on the last day of a short February.
	/// </summary>
	[Fact]
	public void Schedule_QuarterlyAndYearly_StepByPeriod()
	{
		var quarterly = Valid(300m, new DateOnly(2026, 11, 30), RepaymentFrequency.Quarterly, null, 100m, null);
		var yearly = Valid(300m, new DateOnly(2028, 2, 29), RepaymentFrequency.Yearly, null, 100m, null);

		quarterly.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2026, 11, 30), new DateOnly(2027, 2, 28), new DateOnly(2027, 5, 30));
		yearly.Schedule().Select(p => p.Due).Should().Equal(new DateOnly(2028, 2, 29), new DateOnly(2029, 2, 28), new DateOnly(2030, 2, 28));
	}
	#endregion
}
