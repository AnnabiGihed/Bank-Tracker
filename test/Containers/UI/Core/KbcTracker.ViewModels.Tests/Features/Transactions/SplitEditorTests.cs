namespace KbcTracker.ViewModels.Tests.Features.Transactions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="SplitEditor"/>: the first part takes what the others leave, the amounts are read to
///              the cent with a comma or a dot, the parts must add up to the payment exactly, there are 2 to 100 of them,
///              each above zero with a category, and the requests it builds.
/// </summary>
public sealed class SplitEditorTests
{
	#region Test Infrastructure
	/// <summary>Builds the editor of a 12.50 euro payment, split already when parts are given.</summary>
	private static SplitEditor Editor(IReadOnlyList<SplitPartSeed>? seed = null)
	{
		var analysis = TestWorkspace.Analyse([TestWorkspace.Payment("t1", new DateOnly(2026, 10, 3), -12.5m, "Shop One")], (user, _) =>
		{
			if (seed is not null)
			{
				user.SplitPayment(new TxnId("t1"), PaymentSplit.Create(-12.5m, seed.Select(s => new SplitLine(s.Amount, new CategoryId(s.CategoryId), s.Label))).Value);
			}
		});
		var transactions = new TransactionsViewModel(analysis, "2026-10", Formats.Belgian);
		return transactions.OpenSplit(transactions.AllRows.Single());
	}
	#endregion

	#region Start Tests
	/// <summary>Verifies a new split starts with two parts, the first holding the whole payment, and nothing left to assign.</summary>
	[Fact]
	public void New_TwoPartsFirstTakesTheWholePayment()
	{
		var editor = Editor();

		editor.Parts.Should().HaveCount(2);
		editor.Parts[0].AmountText.Should().Be("12.50");
		editor.Parts[1].AmountText.Should().BeEmpty();
		editor.IsBalanced.Should().BeTrue();
		editor.LeftText.Should().Be("Everything is assigned");
		editor.IsValid.Should().BeFalse("the second part is still zero");
		editor.IsSplitAlready.Should().BeFalse();
		editor.TotalText.Should().Be("12,50 €");
		editor.Name.Should().Be("Shop One");
	}

	/// <summary>Verifies an existing split starts from its parts as positive amounts and can be removed.</summary>
	[Fact]
	public void Existing_StartsFromItsPartsAndCanBeRemoved()
	{
		var editor = Editor([new SplitPartSeed(10m, "bills", "Water"), new SplitPartSeed(2.5m, "cash", null)]);

		editor.IsSplitAlready.Should().BeTrue();
		editor.Parts.Select(p => (p.AmountText, p.CategoryId, p.Label)).Should().Equal(("10.00", "bills", "Water"), ("2.50", "cash", string.Empty));
		editor.IsValid.Should().BeTrue();
		editor.UnsplitRequest().Should().Be(new DecisionRequest("unsplit", Id: "t1"));
	}
	#endregion

	#region Amount Tests
	/// <summary>Verifies typing the second part makes the first take the rest, until the owner types in the first.</summary>
	[Fact]
	public void SetAmount_SecondPart_FirstTakesTheRestUntilTypedIn()
	{
		var editor = Editor();

		editor.SetAmount(editor.Parts[1], "2,5");
		var firstAfterSecond = editor.Parts[0].AmountText;
		editor.SetAmount(editor.Parts[0], "5");
		editor.SetAmount(editor.Parts[1], "3");

		firstAfterSecond.Should().Be("10.00");
		editor.Parts[0].AmountText.Should().Be("5");
	}

	/// <summary>Verifies what is left to assign, and what is too much, is said in euro.</summary>
	[Fact]
	public void LeftText_LeftAndTooMuch_InEuro()
	{
		var editor = Editor();
		editor.SetAmount(editor.Parts[0], "5");
		editor.SetAmount(editor.Parts[1], "2,5");
		var left = (editor.LeftCents, editor.LeftText, editor.IsValid);
		editor.SetAmount(editor.Parts[1], "20");

		left.Should().Be((500L, "Left to assign: 5,00 €", false));
		editor.LeftCents.Should().Be(-1250L);
		editor.LeftText.Should().Be("Too much: 12,50 €");
		editor.IsValid.Should().BeFalse();
	}

	/// <summary>Verifies a comma or a dot reads as the decimal separator, amounts are rounded to the cent, and text that is not a number counts as zero.</summary>
	/// <param name="text">The amount as typed.</param>
	/// <param name="cents">The cents it is read as.</param>
	[Theory]
	[InlineData("2,5", 250)]
	[InlineData("2.50", 250)]
	[InlineData(" 7 ", 700)]
	[InlineData("1.005", 101)]
	[InlineData("", 0)]
	[InlineData("abc", 0)]
	[InlineData("-1", -100)]
	public void Cents_TypedAmount_ReadToTheCent(string text, long cents)
	{
		new SplitPartEdit { AmountText = text }.Cents.Should().Be(cents);
	}
	#endregion

	#region Validity Tests
	/// <summary>Verifies a balanced split with every part above zero and a category is valid and builds the request with exact amounts and trimmed labels.</summary>
	[Fact]
	public void ToRequest_ValidSplit_ExactAmountsAndTrimmedLabels()
	{
		var editor = Editor();
		editor.SetAmount(editor.Parts[1], "2,5");
		editor.Parts[0].Label = "  Water bottle  ";
		editor.Parts[1].CategoryId = "cash";

		var request = editor.ToRequest();

		editor.IsValid.Should().BeTrue();
		request.Action.Should().Be("split");
		request.Id.Should().Be("t1");
		request.Parts.Should().Equal(new SplitPartRequest(10m, editor.Parts[0].CategoryId, "Water bottle"), new SplitPartRequest(2.5m, "cash"));
	}

	/// <summary>Verifies a split that is not valid builds no request.</summary>
	[Fact]
	public void ToRequest_InvalidSplit_Throws()
	{
		var editor = Editor();

		var act = () => editor.ToRequest();

		act.Should().Throw<InvalidOperationException>();
	}

	/// <summary>Verifies a part without a category, with a negative amount, or with a label over 100 characters makes the split invalid.</summary>
	[Fact]
	public void IsValid_PartWithoutCategoryNegativeOrLongLabel_False()
	{
		var editor = Editor();
		editor.SetAmount(editor.Parts[1], "2,5");
		var valid = editor.IsValid;

		editor.Parts[1].CategoryId = string.Empty;
		var withoutCategory = editor.IsValid;
		editor.Parts[1].CategoryId = "cash";
		editor.Parts[0].Label = new string('x', 101);
		var longLabel = editor.IsValid;
		editor.Parts[0].Label = new string('x', 100);
		var maxLabel = editor.IsValid;
		editor.SetAmount(editor.Parts[1], "-2,5");
		var negative = editor.IsValid;

		valid.Should().BeTrue();
		withoutCategory.Should().BeFalse();
		longLabel.Should().BeFalse();
		maxLabel.Should().BeTrue();
		negative.Should().BeFalse();
	}

	/// <summary>Verifies at least two parts are needed, so removing one leaves the split invalid.</summary>
	[Fact]
	public void RemovePart_LeavingOnePart_Invalid()
	{
		var editor = Editor([new SplitPartSeed(10m, "bills", null), new SplitPartSeed(2.5m, "cash", null)]);

		editor.RemovePart(editor.Parts[1]);

		editor.Parts.Should().HaveCount(1);
		editor.IsValid.Should().BeFalse();
	}

	/// <summary>Verifies no more than 100 parts can be added, and a new part starts without an amount.</summary>
	[Fact]
	public void AddPart_UpTo100_ThenRefused()
	{
		var editor = Editor();

		while (editor.CanAddPart)
		{
			editor.AddPart().Should().NotBeNull();
		}

		editor.Parts.Should().HaveCount(100);
		editor.AddPart().Should().BeNull();
		editor.Parts[^1].AmountText.Should().BeEmpty();
		editor.IsValid.Should().BeFalse();
	}
	#endregion
}
