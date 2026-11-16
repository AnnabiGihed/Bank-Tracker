namespace KbcTracker.Components.Tests.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="RepaymentPlanDialog"/>: the fields follow the choices (one payment or instalments, the day of
///              the month hidden for weekly instalments, the amount each time or the number of payments), the live preview
///              words the plan, the problems show while the form is incomplete, and Skip for now only exists for a new debt.
/// </summary>
public sealed class RepaymentPlanDialogTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds the form for a credit of 1000 euro from the friend.</summary>
	private static RepaymentPlanEdit FriendForm()
	{
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 5), 1000m)]);
		return RepaymentPlanEdit.ForCredit(analysis, analysis.UnplannedLoans.Single(), Formats.Belgian);
	}

	/// <summary>Renders the dialog on a form.</summary>
	private IRenderedComponent<RepaymentPlanDialog> RenderDialog(RepaymentPlanEdit edit) => Render<RepaymentPlanDialog>(p => p.Add(d => d.Edit, edit));
	#endregion

	#region Toggle Tests
	/// <summary>Verifies choosing one payment hides the instalment fields and relabels the date, and choosing instalments brings them back.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Shape_OnePaymentThenInstalments_TogglesTheFields()
	{
		var dialog = RenderDialog(FriendForm());
		dialog.Markup.Should().Contain("First payment").And.Contain("How often");

		var shape = dialog.FindComponent<RadzenRadioButtonList<RepaymentShape>>();
		await dialog.InvokeAsync(() => shape.Instance.ValueChanged.InvokeAsync(RepaymentShape.OnePayment));

		dialog.Markup.Should().Contain("Due date").And.NotContain("How often");
		dialog.Find("[data-testid='plan-preview']").TextContent.Should().Contain("on 5 Nov 2026");

		await dialog.InvokeAsync(() => shape.Instance.ValueChanged.InvokeAsync(RepaymentShape.Instalments));

		dialog.Markup.Should().Contain("How often");
	}

	/// <summary>Verifies the day of the month is shown from the first date and hidden when the instalments are weekly.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Frequency_Weekly_HidesTheDayOfMonth()
	{
		var dialog = RenderDialog(FriendForm());
		dialog.Find("[data-testid='plan-day']").TextContent.Should().Contain("day 5");

		var frequency = dialog.FindComponent<RadzenDropDown<RepaymentFrequency>>();
		await dialog.InvokeAsync(() => frequency.Instance.ValueChanged.InvokeAsync(RepaymentFrequency.Weekly));

		dialog.FindAll("[data-testid='plan-day']").Should().BeEmpty();
	}

	/// <summary>Verifies sizing by the number of payments swaps the amount field for the count and the preview follows.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Input_Count_SwapsTheFieldAndFeedsThePreview()
	{
		var dialog = RenderDialog(FriendForm());
		dialog.Markup.Should().Contain("Amount each time (€)");

		var input = dialog.FindComponent<RadzenRadioButtonList<InstalmentInput>>();
		await dialog.InvokeAsync(() => input.Instance.ValueChanged.InvokeAsync(InstalmentInput.Count));
		dialog.Markup.Should().Contain("Number of payments").And.NotContain("Amount each time (€)");

		var count = dialog.FindComponent<RadzenNumeric<int?>>();
		await dialog.InvokeAsync(() => count.Instance.ValueChanged.InvokeAsync(4));

		dialog.Find("[data-testid='plan-preview']").TextContent.Should().Be($"4 × {Formats.Belgian.Money(250m)}, 5 Nov 2026 – 5 Feb 2027");
	}

	/// <summary>Verifies the preview follows the amount of each payment and words a smaller last payment.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Preview_AmountEach_WordsTheLastPayment()
	{
		var dialog = RenderDialog(FriendForm());
		dialog.FindAll("[data-testid='plan-preview']").Should().BeEmpty("no amount of each payment is given yet");

		var each = dialog.FindComponents<RadzenNumeric<decimal?>>()[^1];
		await dialog.InvokeAsync(() => each.Instance.ValueChanged.InvokeAsync(300m));

		dialog.Find("[data-testid='plan-preview']").TextContent.Should().Be($"4 × {Formats.Belgian.Money(300m)}, last {Formats.Belgian.Money(100m)}, 5 Nov 2026 – 5 Feb 2027");
	}
	#endregion

	#region Validation Tests
	/// <summary>Verifies the problems show while the form is incomplete and Save is disabled, and Skip for now stays possible.</summary>
	[Fact]
	public void Problems_Incomplete_ShownSaveDisabledSkipEnabled()
	{
		var dialog = RenderDialog(FriendForm());

		dialog.Find("[data-testid='plan-problems']").TextContent.Should().Contain(RepaymentPlanEdit.EachAmountMissing);
		ButtonWithText(dialog, "Save").HasAttribute("disabled").Should().BeTrue();
		ButtonWithText(dialog, "Skip for now").HasAttribute("disabled").Should().BeFalse();
	}

	/// <summary>Verifies an existing debt has no Skip for now.</summary>
	[Fact]
	public void Skip_ExistingDebt_NotOffered()
	{
		var plan = DebtTestData.Monthly(1000m, new DateOnly(2026, 11, 5), 250m);
		var analysis = DebtTestData.Analyse([DebtTestData.FriendCredit("c1", new DateOnly(2026, 10, 5), 1000m)], ("d1", DebtTestData.FriendTerms(plan, principal: 1000m)));
		var dialog = RenderDialog(RepaymentPlanEdit.ForDebt(analysis, analysis.Debts.Single().Debt, Formats.Belgian));

		dialog.FindAll("button").Select(b => b.TextContent.Trim()).Should().NotContain("Skip for now");
		ButtonWithText(dialog, "Save").HasAttribute("disabled").Should().BeFalse();
		dialog.Find("[data-testid='plan-preview']").TextContent.Should().Contain("4 ×");
	}
	#endregion
}
