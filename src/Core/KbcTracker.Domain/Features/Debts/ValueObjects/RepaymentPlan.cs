namespace KbcTracker.Domain.Features.Debts.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : How a debt is repaid (ADR-0010): one payment on a date, or instalments with a first date, a
///              frequency, a day of the month, and the amount of each instalment or their number.
///
///              Immutable. <see cref="Amount"/> is the total to repay and may include interest. Every instalment is
///              <see cref="InstalmentAmount"/> except the last, which is what remains and so can differ. A day of the
///              month the month lacks falls on its last day.
/// </summary>
public sealed class RepaymentPlan
{
	#region Constants
	/// <summary>
	/// The most instalments a plan has.
	/// </summary>
	public const int MaxCount = 600;

	/// <summary>
	/// The earliest year a first date can have.
	/// </summary>
	public const int MinYear = 2000;

	/// <summary>
	/// The latest year a first date can have, so that the last of 600 instalments still has a valid date.
	/// </summary>
	public const int MaxYear = 2200;

	/// <summary>
	/// The number of days between two weekly instalments.
	/// </summary>
	private const int DaysPerWeek = 7;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a plan from values that are already valid or stored.
	/// </summary>
	private RepaymentPlan(decimal amount, DateOnly start, RepaymentFrequency? frequency, int? dayOfMonth, decimal instalmentAmount, int count)
	{
		Amount = amount;
		Start = start;
		Frequency = frequency;
		DayOfMonth = dayOfMonth;
		InstalmentAmount = instalmentAmount;
		Count = count;
	}
	#endregion

	#region Properties
	/// <summary>
	/// Gets the total to repay, interest included.
	/// </summary>
	public decimal Amount { get; }

	/// <summary>
	/// Gets the due date of a single payment, or the first date instalments count from.
	/// </summary>
	public DateOnly Start { get; }

	/// <summary>
	/// Gets how often an instalment falls due, or <see langword="null"/> for one payment.
	/// </summary>
	public RepaymentFrequency? Frequency { get; }

	/// <summary>
	/// Gets the day of the month instalments fall on, or <see langword="null"/> to use the day of <see cref="Start"/>;
	/// always <see langword="null"/> for one payment and for weekly instalments.
	/// </summary>
	public int? DayOfMonth { get; }

	/// <summary>
	/// Gets the amount of every instalment but the last.
	/// </summary>
	public decimal InstalmentAmount { get; }

	/// <summary>
	/// Gets the number of payments; 1 for a single payment.
	/// </summary>
	public int Count { get; }

	/// <summary>
	/// Gets a value indicating whether the debt is repaid in one payment.
	/// </summary>
	public bool IsOnePayment => Frequency is null;

	/// <summary>
	/// Gets the amount of the last instalment: what remains after the others.
	/// </summary>
	public decimal LastInstalmentAmount => Amount - (InstalmentAmount * (Count - 1));
	#endregion

	#region Factory Methods
	/// <summary>
	/// Creates a plan that repays everything in one payment.
	/// </summary>
	/// <param name="amount">The amount to repay.</param>
	/// <param name="dueDate">The date it is due.</param>
	/// <returns>The plan, or <see cref="RepaymentPlanErrors.AmountInvalid"/> or <see cref="RepaymentPlanErrors.StartInvalid"/>.</returns>
	public static Result<RepaymentPlan> OnePayment(decimal amount, DateOnly dueDate)
	{
		if (!IsMoney(amount))
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.AmountInvalid);
		}

		return dueDate.Year is < MinYear or > MaxYear
			? Result.Failure<RepaymentPlan>(RepaymentPlanErrors.StartInvalid)
			: new RepaymentPlan(amount, dueDate, null, null, amount, 1);
	}

	/// <summary>
	/// Creates a plan of instalments. Give either the instalment amount (the count is the amount divided by it,
	/// rounded up) or the count (the instalment is the amount divided by it, rounded up to the cent); the last
	/// instalment takes what remains.
	/// </summary>
	/// <param name="amount">The total to repay.</param>
	/// <param name="start">The first date; the first instalment is the first one on or after it.</param>
	/// <param name="frequency">How often an instalment falls due.</param>
	/// <param name="dayOfMonth">The day of the month (1 to 31), or <see langword="null"/> for the day of <paramref name="start"/>;
	/// must be <see langword="null"/> for weekly instalments.</param>
	/// <param name="instalmentAmount">The amount of each instalment, or <see langword="null"/> to give the count.</param>
	/// <param name="count">The number of instalments (1 to 600), or <see langword="null"/> to give the instalment amount.</param>
	/// <returns>The plan, or the <see cref="RepaymentPlanErrors"/> that rejects it.</returns>
	public static Result<RepaymentPlan> Instalments(decimal amount, DateOnly start, RepaymentFrequency frequency, int? dayOfMonth, decimal? instalmentAmount, int? count)
	{
		if (!IsMoney(amount))
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.AmountInvalid);
		}

		if (start.Year is < MinYear or > MaxYear)
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.StartInvalid);
		}

		if (dayOfMonth is not null && (frequency == RepaymentFrequency.Weekly || dayOfMonth is < 1 or > 31))
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.DayOfMonthInvalid);
		}

		if ((instalmentAmount is null) == (count is null))
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.InstalmentChoice);
		}

		return instalmentAmount is { } each
			? FromInstalmentAmount(amount, start, frequency, dayOfMonth, each)
			: FromCount(amount, start, frequency, dayOfMonth, count!.Value);
	}

	/// <summary>
	/// Rebuilds a stored plan without the checks: stored decisions are kept as they are.
	/// </summary>
	/// <param name="amount">The stored total to repay.</param>
	/// <param name="start">The stored first date.</param>
	/// <param name="frequency">The stored frequency, or <see langword="null"/> for one payment.</param>
	/// <param name="dayOfMonth">The stored day of the month.</param>
	/// <param name="instalmentAmount">The stored instalment amount.</param>
	/// <param name="count">The stored number of payments.</param>
	/// <returns>The plan.</returns>
	public static RepaymentPlan Rehydrate(decimal amount, DateOnly start, RepaymentFrequency? frequency, int? dayOfMonth, decimal instalmentAmount, int count) =>
		new(amount, start, frequency, dayOfMonth, instalmentAmount, count);
	#endregion

	#region Public Methods
	/// <summary>
	/// Lists the payments the plan asks for, by due date.
	/// </summary>
	/// <returns>One entry per payment; their amounts add up to <see cref="Amount"/>.</returns>
	public IReadOnlyList<ScheduledPayment> Schedule()
	{
		var payments = new List<ScheduledPayment>(Count);
		for (var index = 0; index < Count; index++)
		{
			payments.Add(new ScheduledPayment(DueDate(index), index == Count - 1 ? LastInstalmentAmount : InstalmentAmount));
		}

		return payments;
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Determines whether an amount is positive with at most two decimals.
	/// </summary>
	private static bool IsMoney(decimal amount) => amount > 0 && decimal.Round(amount, 2) == amount;

	/// <summary>
	/// Builds the plan from the amount of each instalment.
	/// </summary>
	private static Result<RepaymentPlan> FromInstalmentAmount(decimal amount, DateOnly start, RepaymentFrequency frequency, int? dayOfMonth, decimal each)
	{
		if (!IsMoney(each) || each > amount)
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.InstalmentAmountInvalid);
		}

		var count = Math.Ceiling(amount / each);
		return count > MaxCount
			? Result.Failure<RepaymentPlan>(RepaymentPlanErrors.CountInvalid)
			: new RepaymentPlan(amount, start, frequency, dayOfMonth, each, (int)count);
	}

	/// <summary>
	/// Builds the plan from the number of instalments.
	/// </summary>
	private static Result<RepaymentPlan> FromCount(decimal amount, DateOnly start, RepaymentFrequency frequency, int? dayOfMonth, int count)
	{
		if (count is < 1 or > MaxCount)
		{
			return Result.Failure<RepaymentPlan>(RepaymentPlanErrors.CountInvalid);
		}

		var each = Math.Ceiling(amount / count * 100) / 100;
		return each * (count - 1) >= amount
			? Result.Failure<RepaymentPlan>(RepaymentPlanErrors.CountInvalid)
			: new RepaymentPlan(amount, start, frequency, dayOfMonth, each, count);
	}

	/// <summary>
	/// Calculates the due date of one payment, counting from zero.
	/// </summary>
	private DateOnly DueDate(int index)
	{
		if (Frequency is null)
		{
			return Start;
		}

		if (Frequency == RepaymentFrequency.Weekly)
		{
			return Start.AddDays(DaysPerWeek * index);
		}

		var step = Frequency switch { RepaymentFrequency.Monthly => 1, RepaymentFrequency.Quarterly => 3, _ => 12 };
		var day = DayOfMonth ?? Start.Day;
		var firstMonth = new DateOnly(Start.Year, Start.Month, 1);

		// The month of the start date may have its day before the start date: the first instalment is then a period later.
		var offset = OnDay(firstMonth, day) < Start ? 1 : 0;
		return OnDay(firstMonth.AddMonths((index + offset) * step), day);
	}

	/// <summary>
	/// Gives the day of a month, or the last day of the month when it is shorter.
	/// </summary>
	private static DateOnly OnDay(DateOnly month, int day) =>
		new(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
	#endregion
}
