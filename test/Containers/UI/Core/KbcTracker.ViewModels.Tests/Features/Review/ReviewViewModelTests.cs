namespace KbcTracker.ViewModels.Tests.Features.Review;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="ReviewViewModel"/> as data: the payees whose category is automatic, the direction
///              filter, and the request that confirms every payee shown.
/// </summary>
public sealed class ReviewViewModelTests
{
	#region Test Infrastructure
	/// <summary>Builds payees the app has not confirmed: one the owner pays and one that pays the owner.</summary>
	private static Analysis Unconfirmed() => TestWorkspace.Analyse(
	[
		TestWorkspace.Payment("t1", new DateOnly(2026, 10, 5), -8m, "Rail Co"),
		TestWorkspace.Payment("t2", new DateOnly(2026, 10, 6), -3m, "Bakery"),
		TestWorkspace.Payment("t3", new DateOnly(2026, 10, 1), 40m, "Friend"),
	]);
	#endregion

	#region Review Tests
	/// <summary>Verifies the rows mirror the analysis's payees to review, with total, last date and the direction of the money.</summary>
	[Fact]
	public void Rows_MirrorTheAnalysisReviewList()
	{
		var analysis = Unconfirmed();

		var review = new ReviewViewModel(analysis, Formats.Belgian);

		analysis.Review.Should().NotBeEmpty();
		review.IsEmpty.Should().BeFalse();
		review.VisibleRows.Select(r => r.SeriesKey).Should().Equal(analysis.Review.Select(r => r.SeriesKey));
		var first = analysis.Review[0];
		var row = review.VisibleRows[0];
		row.Name.Should().Be(first.Name);
		row.Count.Should().Be(first.Count);
		row.TotalText.Should().Be(Formats.Belgian.Money(first.Total));
		row.LastText.Should().Be(Formats.Belgian.ShortDate(first.Last));
		row.TotalIsPositive.Should().Be(first.Total > 0);
		row.Incoming.Should().Be(first.Incoming);
	}

	/// <summary>Verifies the direction filter splits the payees into money out and money in, and "all" shows both.</summary>
	[Fact]
	public void Direction_OutAndIn_PartitionTheRows()
	{
		var review = new ReviewViewModel(Unconfirmed(), Formats.Belgian);
		var all = review.VisibleRows.Count;

		review.Direction = DirectionFilter.Out;
		var outgoing = review.VisibleRows.ToList();
		review.Direction = DirectionFilter.In;
		var incoming = review.VisibleRows.ToList();

		outgoing.Should().OnlyContain(r => !r.Incoming).And.NotBeEmpty();
		incoming.Should().OnlyContain(r => r.Incoming).And.NotBeEmpty();
		(outgoing.Count + incoming.Count).Should().Be(all);
	}

	/// <summary>Verifies confirming all payees shown sends the keys of those shown only, with the old question.</summary>
	[Fact]
	public void ConfirmShown_FilteredRows_KeysOfThoseShown()
	{
		var review = new ReviewViewModel(Unconfirmed(), Formats.Belgian) { Direction = DirectionFilter.In };

		var request = review.ConfirmShownRequest();

		request.Action.Should().Be("confirm");
		request.Keys.Should().Equal(review.VisibleRows.Select(r => r.SeriesKey));
		review.ConfirmShownPrompt().Should().Be($"Confirm the category of {review.VisibleRows.Count} payees?");
	}

	/// <summary>Verifies a row's requests: confirm by key, and another category for all the payee's payments.</summary>
	[Fact]
	public void RowRequests_ConfirmAndChangeCategory_OldShape()
	{
		var row = new ReviewViewModel(Unconfirmed(), Formats.Belgian).VisibleRows[0];

		row.ConfirmRequest.Should().Be(new DecisionRequest("confirm", Key: row.SeriesKey));
		row.ChangeCategoryRequest("bills").Should().Be(new DecisionRequest("payeeCategory", Key: row.SeriesKey, CategoryId: "bills"));
	}

	/// <summary>Verifies nothing to review is reported as empty, with the old message.</summary>
	[Fact]
	public void IsEmpty_NothingToReview_True()
	{
		var review = new ReviewViewModel(TestWorkspace.Analyse([]), Formats.Belgian);

		review.IsEmpty.Should().BeTrue();
		review.VisibleRows.Should().BeEmpty();
		ReviewViewModel.EmptyMessage.Should().StartWith("All categories are confirmed.");
	}
	#endregion
}
