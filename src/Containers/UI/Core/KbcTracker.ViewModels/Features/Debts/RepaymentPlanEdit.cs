using KbcTracker.Domain.Features.Debts.Errors;
using Pivot.Framework.Domain.Shared;
using TxnKind = KbcTracker.Domain.Features.Transactions.ValueObjects.Kind;

namespace KbcTracker.ViewModels.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The form that says who lent money and how it is repaid (ADR-0010): the kind (person or bank), the lender and
///              IBAN, the day the money was borrowed, the amount to repay, then either one payment on a date or instalments
///              (first date, how often, and the amount each time or the number of payments). It starts from a loan credit
///              (prefilled from the payer and the payment), from an existing debt, or empty for a loan that has no credit
///              in the bank's history. It checks with the domain's own rules, words the plan as a live preview and builds
///              the <c>addDebt</c> or <c>updateDebt</c> request, or the same without a plan ("Skip for now").
/// </summary>
public sealed class RepaymentPlanEdit
{
	#region Constants
	/// <summary>The words that ask for the amount of each payment.</summary>
	public const string EachAmountMissing = "Enter the amount of each payment.";

	/// <summary>The words that ask for the number of payments.</summary>
	public const string CountMissing = "Enter the number of payments.";

	/// <summary>The words that ask for the due date of a single payment.</summary>
	public const string DueDateMissing = "Pick the due date.";

	/// <summary>The words that ask for the date of the first instalment.</summary>
	public const string FirstDateMissing = "Pick the date of the first payment.";

	/// <summary>The number of months a plan's first date lies after the day the money was borrowed, until the owner picks one.</summary>
	private const int DefaultMonthsToFirstPayment = 1;
	#endregion

	#region Fields
	/// <summary>The formats of amounts.</summary>
	private readonly Formats _formats;

	/// <summary>The amount received of a loan without a credit, kept when its plan changes; <see langword="null"/> otherwise.</summary>
	private readonly decimal? _principal;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="RepaymentPlanEdit"/>; the factory methods fill it.</summary>
	private RepaymentPlanEdit(Formats formats, string? debtId, string? receivedPaymentId, decimal? principal, IReadOnlyList<LoanSeriesChoice> seriesChoices)
	{
		_formats = formats;
		DebtId = debtId;
		ReceivedPaymentId = receivedPaymentId;
		_principal = principal;
		SeriesChoices = seriesChoices;
	}
	#endregion

	#region Properties
	/// <summary>Gets the debt being edited, or <see langword="null"/> for a new one.</summary>
	public string? DebtId { get; }

	/// <summary>Gets the credit that paid the money out, or <see langword="null"/> for a loan older than the bank's history.</summary>
	public string? ReceivedPaymentId { get; }

	/// <summary>Gets a value indicating whether the form adds a debt; only then "Skip for now" is offered.</summary>
	public bool IsNew => DebtId is null;

	/// <summary>Gets the title of the dialog.</summary>
	public string Title => IsNew ? "How is this repaid?" : "Edit the repayment plan";

	/// <summary>Gets or sets whether the money came from a person or a bank.</summary>
	public DebtKind Kind { get; set; }

	/// <summary>Gets or sets who lent the money.</summary>
	public string Lender { get; set; } = string.Empty;

	/// <summary>Gets or sets the lender's IBAN, which identifies the repayments to a person; may stay empty.</summary>
	public string Iban { get; set; } = string.Empty;

	/// <summary>Gets or sets the day the money was received; only payments from that day on can repay the debt.</summary>
	public DateOnly? BorrowedOn { get; set; }

	/// <summary>Gets or sets the total to repay, which for a bank loan may include interest.</summary>
	public decimal? AmountToRepay { get; set; }

	/// <summary>Gets or sets whether the debt is repaid in one payment or in instalments.</summary>
	public RepaymentShape Shape { get; set; } = RepaymentShape.Instalments;

	/// <summary>Gets or sets the due date of the single payment, or the date of the first instalment.</summary>
	public DateOnly? PaymentDate { get; set; }

	/// <summary>Gets or sets how often an instalment falls due.</summary>
	public RepaymentFrequency Frequency { get; set; } = RepaymentFrequency.Monthly;

	/// <summary>Gets or sets whether the instalments are sized by their amount or by their number.</summary>
	public InstalmentInput Input { get; set; } = InstalmentInput.AmountEach;

	/// <summary>Gets or sets the amount of each instalment, used when <see cref="Input"/> is <see cref="InstalmentInput.AmountEach"/>.</summary>
	public decimal? InstalmentAmount { get; set; }

	/// <summary>Gets or sets the number of instalments, used when <see cref="Input"/> is <see cref="InstalmentInput.Count"/>.</summary>
	public int? Count { get; set; }

	/// <summary>Gets or sets the <c>LOAN:</c> series of a bank loan's instalments, or <see langword="null"/> for none.</summary>
	public string? SeriesKey { get; set; }

	/// <summary>Gets the series a bank loan can be linked to.</summary>
	public IReadOnlyList<LoanSeriesChoice> SeriesChoices { get; }

	/// <summary>Gets a value indicating whether the shape is instalments, which ask for a frequency and a size.</summary>
	public bool IsInstalments => Shape == RepaymentShape.Instalments;

	/// <summary>Gets a value indicating whether the day of the month is shown: monthly, quarterly and yearly instalments.</summary>
	public bool ShowsDayOfMonth => IsInstalments && Frequency != RepaymentFrequency.Weekly && PaymentDate is not null;

	/// <summary>Gets the day of the month instalments fall on, taken from the first date, or <see langword="null"/> when it does not apply.</summary>
	public int? DayOfMonth => ShowsDayOfMonth ? PaymentDate!.Value.Day : null;

	/// <summary>Gets a value indicating whether the series picker is shown: for a bank loan.</summary>
	public bool ShowsSeries => Kind == DebtKind.BankLoan;

	/// <summary>Gets the label of the date field: the due date, or the first payment.</summary>
	public string DateLabel => IsInstalments ? "First payment" : "Due date";

	/// <summary>Gets the hint under the amount to repay.</summary>
	public string AmountHint => Kind == DebtKind.BankLoan
		? "A bank loan's total may include interest, so it can be more than the amount you received."
		: "What you will pay back in all, usually the amount you received.";

	/// <summary>Gets the live preview of the plan, or <see langword="null"/> while the plan is not valid.</summary>
	public string? PreviewText => BuildPlan() is { IsSuccess: true } plan ? RepaymentPlanText.Describe(plan.Value, _formats) : null;

	/// <summary>Gets what is wrong with the form, in words, one entry each; empty when it can be saved.</summary>
	public IReadOnlyList<string> Problems => [.. DebtProblems(), .. PlanProblems()];

	/// <summary>Gets a value indicating whether the form can be saved with its plan.</summary>
	public bool IsValid => Problems.Count == 0;

	/// <summary>Gets a value indicating whether the debt can be saved without a plan: the lender, IBAN, day borrowed and, for a loan without a credit, the amount are fine.</summary>
	public bool CanSkip => DebtProblems().Count == 0;
	#endregion

	#region Factory Methods
	/// <summary>Starts the form for a loan credit that has no debt yet.</summary>
	/// <param name="analysis">The analysis, which lists the loan series.</param>
	/// <param name="credit">The credit that paid the money out.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>The form: kind from the category, lender and IBAN from the payer, borrowed on the credit's date, the amount of the credit, a plan of
	/// monthly instalments from a month later, and the loan series when exactly one has payments after the credit.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static RepaymentPlanEdit ForCredit(Analysis analysis, TxnInfo credit, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(credit);
		ArgumentNullException.ThrowIfNull(formats);
		var edit = new RepaymentPlanEdit(formats, null, credit.T.Id.Value, null, SeriesOf(analysis, formats))
		{
			Kind = analysis.Tree.Path(credit.Category.Id.Value).Any(c => c.Id == CategoryId.BorrowedPeople) ? DebtKind.PersonalLoan : DebtKind.BankLoan,
			Lender = credit.Name,
			Iban = credit.T.CounterpartyIban ?? string.Empty,
			BorrowedOn = credit.T.Date,
			AmountToRepay = credit.T.Amount,
			PaymentDate = credit.T.Date.AddMonths(DefaultMonthsToFirstPayment),
		};
		edit.SeriesKey = edit.Kind == DebtKind.BankLoan ? OnlySeriesAfter(analysis, credit.T.Date) : null;
		return edit;
	}

	/// <summary>Starts the form for a loan that has no credit in the bank's history.</summary>
	/// <param name="analysis">The analysis, which lists the loan series.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>An empty form for a personal loan.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static RepaymentPlanEdit ForNewDebt(Analysis analysis, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(formats);
		return new RepaymentPlanEdit(formats, null, null, null, SeriesOf(analysis, formats)) { Kind = DebtKind.PersonalLoan };
	}

	/// <summary>Starts the form for an existing debt.</summary>
	/// <param name="analysis">The analysis, which lists the loan series.</param>
	/// <param name="debt">The debt.</param>
	/// <param name="formats">The formats of amounts.</param>
	/// <returns>The form with what is stored; a debt without a plan gets monthly instalments from a month after it was borrowed.</returns>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public static RepaymentPlanEdit ForDebt(Analysis analysis, Debt debt, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(analysis);
		ArgumentNullException.ThrowIfNull(debt);
		ArgumentNullException.ThrowIfNull(formats);
		var plan = debt.Plan;
		var edit = new RepaymentPlanEdit(formats, debt.Id.Value, debt.ReceivedPaymentId?.Value, debt.ReceivedPaymentId is null ? debt.Principal : null, SeriesOf(analysis, formats))
		{
			Kind = debt.Kind,
			Lender = debt.Lender,
			Iban = debt.Iban ?? string.Empty,
			BorrowedOn = debt.BorrowedOn,
			AmountToRepay = debt.AmountToRepay,
			SeriesKey = debt.SeriesKey,
			Shape = plan is { IsOnePayment: true } ? RepaymentShape.OnePayment : RepaymentShape.Instalments,
			PaymentDate = plan?.Start ?? debt.BorrowedOn.AddMonths(DefaultMonthsToFirstPayment),
		};
		if (plan is { IsOnePayment: false })
		{
			edit.Frequency = plan.Frequency!.Value;
			edit.InstalmentAmount = plan.InstalmentAmount;
			edit.Count = plan.Count;
		}

		return edit;
	}
	#endregion

	#region Public Methods
	/// <summary>Builds the request that saves the debt with its plan.</summary>
	/// <returns>The <c>addDebt</c> request for a new debt, otherwise <c>updateDebt</c>.</returns>
	/// <exception cref="InvalidOperationException">The form is not valid.</exception>
	public DecisionRequest ToRequest() => IsValid
		? ToDecision(withPlan: true)
		: throw new InvalidOperationException("The repayment plan has problems: " + string.Join(' ', Problems));

	/// <summary>Builds the request that saves the debt without a plan, which marks it "No plan yet".</summary>
	/// <returns>The <c>addDebt</c> request for a new debt, otherwise <c>updateDebt</c>.</returns>
	/// <exception cref="InvalidOperationException">The lender, IBAN, day borrowed or amount has a problem.</exception>
	public DecisionRequest ToSkipRequest() => CanSkip
		? ToDecision(withPlan: false)
		: throw new InvalidOperationException("The debt has problems: " + string.Join(' ', DebtProblems()));
	#endregion

	#region Private Helpers
	/// <summary>Lists the series of bank loan instalments in the history.</summary>
	private static List<LoanSeriesChoice> SeriesOf(Analysis analysis, Formats formats) =>
		[.. analysis.Items.Where(i => i.Kind == TxnKind.Loan && i.SeriesKey.StartsWith(Debt.LoanSeriesPrefix, StringComparison.Ordinal))
			.GroupBy(i => i.SeriesKey, StringComparer.Ordinal)
			.Select(g => new LoanSeriesChoice(g.Key, $"{g.First().Name} · {g.Key[Debt.LoanSeriesPrefix.Length..]}"))
			.OrderBy(c => c.Text, formats.NameComparer)];

	/// <summary>Gets the loan series that has payments on or after a day, when there is exactly one.</summary>
	private static string? OnlySeriesAfter(Analysis analysis, DateOnly day)
	{
		var series = analysis.Items
			.Where(i => i.Kind == TxnKind.Loan && i.T.Amount < 0 && i.T.Date >= day && i.SeriesKey.StartsWith(Debt.LoanSeriesPrefix, StringComparison.Ordinal))
			.Select(i => i.SeriesKey).Distinct(StringComparer.Ordinal).Take(2).ToList();
		return series.Count == 1 ? series[0] : null;
	}

	/// <summary>Builds the plan the form describes, or <see langword="null"/> while a field the plan needs is empty.</summary>
	private Result<RepaymentPlan>? BuildPlan()
	{
		if (PaymentDate is not { } date)
		{
			return null;
		}

		var amount = AmountToRepay ?? 0m;
		if (!IsInstalments)
		{
			return RepaymentPlan.OnePayment(amount, date);
		}

		if (Input == InstalmentInput.AmountEach ? InstalmentAmount is null : Count is null)
		{
			return null;
		}

		return RepaymentPlan.Instalments(amount, date, Frequency, null,
			Input == InstalmentInput.AmountEach ? InstalmentAmount : null, Input == InstalmentInput.Count ? Count : null);
	}

	/// <summary>Lists the problems with the debt itself: lender, IBAN, day borrowed and, without a credit, the amount.</summary>
	private List<string> DebtProblems()
	{
		var problems = new List<string>();
		var lender = Lender.Trim();
		if (lender.Length is 0 or > Debt.MaxLenderLength)
		{
			problems.Add(DebtErrors.LenderInvalid.Message);
		}

		var iban = Iban.Replace(" ", "", StringComparison.Ordinal);
		if (iban.Length > Debt.MaxIbanLength || !iban.All(char.IsAsciiLetterOrDigit))
		{
			problems.Add(DebtErrors.IbanInvalid.Message);
		}

		if (BorrowedOn is not { } day || day.Year is < RepaymentPlan.MinYear or > RepaymentPlan.MaxYear)
		{
			problems.Add(DebtErrors.BorrowedOnInvalid.Message);
		}

		if (ReceivedPaymentId is null && (AmountToRepay is not { } amount || amount <= 0 || decimal.Round(amount, 2) != amount))
		{
			problems.Add(RepaymentPlanErrors.AmountInvalid.Message);
		}

		return problems;
	}

	/// <summary>Lists the problems with the plan: the date, the size of the payments and what the domain rejects.</summary>
	private List<string> PlanProblems()
	{
		var problems = new List<string>();
		if (PaymentDate is null)
		{
			problems.Add(IsInstalments ? FirstDateMissing : DueDateMissing);
		}

		if (IsInstalments && Input == InstalmentInput.AmountEach && InstalmentAmount is null)
		{
			problems.Add(EachAmountMissing);
		}

		if (IsInstalments && Input == InstalmentInput.Count && Count is null)
		{
			problems.Add(CountMissing);
		}

		if (BuildPlan() is { IsFailure: true } plan)
		{
			problems.Add(plan.Error.Message);
		}

		return problems;
	}

	/// <summary>Builds the request, with or without the plan.</summary>
	private DecisionRequest ToDecision(bool withPlan)
	{
		var request = new DebtRequest(Kind, Lender.Trim(), string.IsNullOrWhiteSpace(Iban) ? null : Iban.Trim(), ReceivedPaymentId,
			ReceivedPaymentId is null ? _principal ?? AmountToRepay : null,
			ShowsSeries && !string.IsNullOrWhiteSpace(SeriesKey) ? SeriesKey : null, withPlan ? ToPlanRequest() : null, BorrowedOn);
		return DebtId is null ? DecisionRequests.AddDebt(request) : DecisionRequests.UpdateDebt(DebtId, request);
	}

	/// <summary>Builds the plan part of the request from a valid form.</summary>
	private RepaymentPlanRequest ToPlanRequest()
	{
		if (!IsInstalments)
		{
			return new(AmountToRepay!.Value, PaymentDate!.Value);
		}

		var each = Input == InstalmentInput.AmountEach ? InstalmentAmount : null;
		var count = Input == InstalmentInput.Count ? Count : null;
		return new(AmountToRepay!.Value, PaymentDate!.Value, Frequency, null, each, count);
	}
	#endregion
}
