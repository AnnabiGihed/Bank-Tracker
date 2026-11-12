namespace KbcTracker.Domain.Tests.Features.Splits.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the rules of <see cref="PaymentSplit"/>: the number of lines, positive amounts with two
///              decimals, trimmed labels, the sum that must equal the payment, and moving lines to another category.
/// </summary>
public sealed class PaymentSplitTests
{
	#region Test Infrastructure
	/// <summary>
	/// Creates a line in a category named by its id.
	/// </summary>
	private static SplitLine Line(decimal amount, string category = "groceries", string? label = null) =>
		new(amount, new CategoryId(category), label);
	#endregion

	#region Create Tests
	/// <summary>
	/// Verifies that parts adding up to the payment, whatever its sign, make a split with trimmed labels and a blank
	/// label turned into none.
	/// </summary>
	/// <param name="paymentAmount">The payment's amount, a debit or a refund.</param>
	[Theory]
	[InlineData(-84.30)]
	[InlineData(84.30)]
	public void Create_PartsAddUp_SplitWithCleanedLabels(double paymentAmount)
	{
		var created = PaymentSplit.Create((decimal)paymentAmount, [Line(50m, label: "  Meat "), Line(34.30m, "personal-care", "   ")]);

		created.IsSuccess.Should().BeTrue();
		created.Value.Lines.Select(l => l.Label).Should().Equal("Meat", null);
		created.Value.Lines.Select(l => l.Amount).Should().Equal(50m, 34.30m);
	}

	/// <summary>
	/// Verifies that fewer than two and more than a hundred parts are refused, and that two and a hundred are fine.
	/// </summary>
	/// <param name="count">The number of parts.</param>
	/// <param name="accepted">Whether the split is accepted.</param>
	[Theory]
	[InlineData(0, false)]
	[InlineData(1, false)]
	[InlineData(2, true)]
	[InlineData(100, true)]
	[InlineData(101, false)]
	public void Create_NumberOfParts_BetweenTwoAndHundred(int count, bool accepted)
	{
		var lines = Enumerable.Range(0, count).Select(_ => Line(1m)).ToList();

		var created = PaymentSplit.Create(-count, lines);

		created.IsSuccess.Should().Be(accepted);
		if (!accepted)
		{
			created.Error.Should().Be(PaymentSplitErrors.LineCount);
		}
	}

	/// <summary>
	/// Verifies that a part that is zero, negative or has more than two decimals is refused.
	/// </summary>
	/// <param name="amount">The part's amount.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-5)]
	[InlineData(5.005)]
	public void Create_InvalidAmount_Refused(double amount)
	{
		var created = PaymentSplit.Create(-10m, [Line((decimal)amount), Line(10m - (decimal)amount)]);

		created.Error.Should().Be(PaymentSplitErrors.AmountInvalid);
	}

	/// <summary>
	/// Verifies that a label of a hundred characters is kept and a longer one refused.
	/// </summary>
	[Fact]
	public void Create_LabelLength_HundredCharactersAtMost()
	{
		var exact = PaymentSplit.Create(-10m, [Line(5m, label: new string('a', 100)), Line(5m)]);
		var tooLong = PaymentSplit.Create(-10m, [Line(5m, label: new string('a', 101)), Line(5m)]);

		exact.IsSuccess.Should().BeTrue();
		tooLong.Error.Should().Be(PaymentSplitErrors.LabelTooLong);
	}

	/// <summary>
	/// Verifies that parts that do not add up to the payment's amount exactly are refused.
	/// </summary>
	[Fact]
	public void Create_SumDiffersFromPayment_Refused()
	{
		var created = PaymentSplit.Create(-84.30m, [Line(50m), Line(34.29m)]);

		created.Error.Should().Be(PaymentSplitErrors.SumMismatch);
	}
	#endregion

	#region Rehydrate Tests
	/// <summary>
	/// Verifies that a stored split is read back as it is, even when it breaks the rules for a new one.
	/// </summary>
	[Fact]
	public void Rehydrate_StoredLines_KeptAsTheyAre()
	{
		var split = PaymentSplit.Rehydrate([Line(5.005m, label: "  odd ")]);

		split.Lines.Should().ContainSingle().Which.Should().Be(Line(5.005m, label: "  odd "));
	}
	#endregion

	#region Reassign Tests
	/// <summary>
	/// Verifies that the lines of the removed category move to the heir, the others stay, and a split without such
	/// a line is returned unchanged.
	/// </summary>
	[Fact]
	public void ReassignCategory_LinesMoved_OthersKept()
	{
		var split = PaymentSplit.Create(-30m, [Line(10m, "bread"), Line(20m, "meat")]).Value;

		var moved = split.ReassignCategory(new CategoryId("bread"), new CategoryId("food"));
		var untouched = split.ReassignCategory(new CategoryId("car"), new CategoryId("food"));

		moved.Lines.Select(l => l.CategoryId.Value).Should().Equal("food", "meat");
		split.Lines.Select(l => l.CategoryId.Value).Should().Equal("bread", "meat");
		untouched.Should().BeSameAs(split);
	}
	#endregion
}
