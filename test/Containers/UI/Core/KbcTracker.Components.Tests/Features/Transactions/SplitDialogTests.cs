namespace KbcTracker.Components.Tests.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SplitDialog"/> opened through the <see cref="DialogService"/>, the way the page opens
///              it: Save is blocked while the parts do not add up exactly, closes with the split decision once they do,
///              Remove split closes with the unsplit decision, and Cancel closes with nothing.
/// </summary>
public sealed class SplitDialogTests : UiTestContext
{
	#region Test Infrastructure
	/// <summary>Builds the editor of the 12.50 euro payment of the analysis, split already when parts are given.</summary>
	private static SplitEditor EditorOf(params SplitLine[] lines)
	{
		var analysis = TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 11, 3), -12.50m, "Shop One")],
			(user, _) =>
			{
				if (lines.Length > 0)
				{
					user.SplitPayment(new TxnId("t1"), PaymentSplit.Create(-12.5m, lines).Value);
				}
			});
		var transactions = new TransactionsViewModel(analysis, "2026-11", Formats.Belgian);
		return transactions.OpenSplit(transactions.AllRows.Single());
	}

	/// <summary>Opens the dialog the way the page does and returns the pending result.</summary>
	private Task<dynamic?> Open(SplitEditor editor) => Services.GetRequiredService<DialogService>().OpenAsync<SplitDialog>("Split",
		new Dictionary<string, object?>(StringComparer.Ordinal) { [nameof(SplitDialog.Editor)] = editor });
	#endregion

	#region Validation Tests
	/// <summary>Verifies Save stays off until the amounts add up exactly, and the remainder text says how far off they are.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Save_PartsDoNotAddUp_Blocked()
	{
		var host = RenderRadzenHost();
		_ = Open(EditorOf());
		await host.WaitForAssertionAsync(() => host.Find("input[aria-label='Amount of part 2']"));

		await host.Find("input[aria-label='Amount of part 2']").InputAsync("5");
		await host.Find("input[aria-label='Amount of part 1']").InputAsync("1");

		host.Find("[role='status']").TextContent.Should().Contain("Left to assign: " + Formats.Belgian.Money(6.50m));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();

		await host.Find("input[aria-label='Amount of part 1']").InputAsync("10");

		host.Find("[role='status']").TextContent.Should().Contain("Too much: " + Formats.Belgian.Money(2.50m));
		ButtonWithText(host, "Save").HasAttribute("disabled").Should().BeTrue();
	}

	/// <summary>Verifies a part can be added and removed, with at least two parts always kept.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Parts_AddedAndRemoved_KeepsAtLeastTwo()
	{
		var host = RenderRadzenHost();
		_ = Open(EditorOf());
		await host.WaitForAssertionAsync(() => host.Find("input[aria-label='Amount of part 2']"));
		host.Find("button[aria-label='Remove part 1']").HasAttribute("disabled").Should().BeTrue();

		await ButtonWithText(host, "+ Add part").ClickAsync();

		host.FindAll("input[aria-label^='Amount of part']").Should().HaveCount(3);
		await host.Find("button[aria-label='Remove part 3']").ClickAsync();
		host.FindAll("input[aria-label^='Amount of part']").Should().HaveCount(2);
	}
	#endregion

	#region Result Tests
	/// <summary>Verifies a balanced split closes the dialog with the decision that splits the payment.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Save_Balanced_ClosesWithTheSplitDecision()
	{
		var host = RenderRadzenHost();
		var result = Open(EditorOf());
		await host.WaitForAssertionAsync(() => host.Find("input[aria-label='Amount of part 2']"));
		await host.Find("input[aria-label='Amount of part 2']").InputAsync("5");
		await host.Find("input[aria-label='Label of part 2']").ChangeAsync("Snacks");

		await ButtonWithText(host, "Save").ClickAsync();

		var decision = (await result as object).Should().BeOfType<DecisionRequest>().Subject;
		decision.Action.Should().Be("split");
		decision.Id.Should().Be("t1");
		decision.Parts.Should().HaveCount(2);
		decision.Parts[0].Amount.Should().Be(7.50m);
		decision.Parts[1].Amount.Should().Be(5m);
		decision.Parts[1].Label.Should().Be("Snacks");
	}

	/// <summary>Verifies a payment that is split already offers Remove split, which closes with the unsplit decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task RemoveSplit_SplitAlready_ClosesWithTheUnsplitDecision()
	{
		var host = RenderRadzenHost();
		var result = Open(EditorOf(new SplitLine(5m, new CategoryId("trav-train"), "Ticket"), new SplitLine(7.5m, new CategoryId("bills"), null)));
		await host.WaitForAssertionAsync(() => ButtonWithText(host, "Remove split"));

		await ButtonWithText(host, "Remove split").ClickAsync();

		var decision = (await result as object).Should().BeOfType<DecisionRequest>().Subject;
		decision.Action.Should().Be("unsplit");
		decision.Id.Should().Be("t1");
	}

	/// <summary>Verifies a payment that is not split has no Remove split button, and Cancel closes with nothing.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Cancel_Clicked_ClosesWithoutADecision()
	{
		var host = RenderRadzenHost();
		var result = Open(EditorOf());
		await host.WaitForAssertionAsync(() => ButtonWithText(host, "Cancel"));
		host.FindAll("button").Should().NotContain(b => string.Equals(b.TextContent.Trim(), "Remove split", StringComparison.Ordinal));

		await ButtonWithText(host, "Cancel").ClickAsync();

		(await result as object).Should().BeNull();
	}
	#endregion
}
