namespace KbcTracker.Domain.Tests.Features.FixedPayments.ValueObjects;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins the rule that tells a subscription from another fixed payment: card payments only, repeating
///              monthly or yearly. Uses invented transactions from <see cref="BankJson"/> and a fixed reference date.
/// </summary>
public sealed class SubscriptionRuleTests
{
	#region Test Infrastructure
	/// <summary>The fixed "today" of every analysis in this class.</summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>Runs the analysis on the given transactions with no user decisions.</summary>
	private static Analysis Analyse(IEnumerable<Txn> txns) =>
		new([.. txns], CategoryDefaults.Create(new Dictionary<string, string[]> { ["Housing"] = ["LANDLORD"] }), UserData.Empty(), Today);

	/// <summary>Three months of a card subscription, a rent transfer and a loan instalment.</summary>
	private static Analysis ThreeMonths() => Analyse(
	[
		BankJson.Card("2026-08-12", 13.99m, "WL*GOOGLE YouTube IED02 R296 Dublin"),
		BankJson.Card("2026-09-14", 13.99m, "WL*GOOGLE YouTube IED02 R301 Dublin"),
		BankJson.Card("2026-10-13", 13.99m, "WL*GOOGLE YouTube IEUNKNOWN Dublin"),
		BankJson.TransferOut("2026-09-02", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.TransferOut("2026-10-01", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.TransferOut("2026-11-02", 940m, "LANDLORD ONE", "BE00111122223333"),
		BankJson.LoanInstalment("2026-09-07", 375.50m, "726-0000000-01"),
		BankJson.LoanInstalment("2026-10-07", 375.50m, "726-0000000-01"),
		BankJson.LoanInstalment("2026-11-09", 375.50m, "726-0000000-01"),
	]);

	/// <summary>Builds one card payment as the analysis sees it.</summary>
	private static TxnInfo CardPayment() => Analyse([BankJson.Card("2026-10-13", 13.99m, "WL*GOOGLE YouTube IEUNKNOWN Dublin")]).Items.Single();
	#endregion

	#region Suggestion Tests
	/// <summary>Verifies that of the three detected suggestions only the monthly card payment is a subscription.</summary>
	[Fact]
	public void Suggestion_CardMonthly_IsSubscriptionAndTransferAndLoanAreNot()
	{
		var suggestions = ThreeMonths().Suggestions;

		suggestions.Where(s => s.IsSubscription).Select(s => s.Amount).Should().Equal(13.99m);
		suggestions.Single(s => s.Amount == 940m).IsSubscription.Should().BeFalse("A transfer is not a subscription.");
		suggestions.Single(s => s.Amount == 375.50m).IsSubscription.Should().BeFalse("A loan instalment is not a subscription.");
	}

	/// <summary>Verifies that an accepted card subscription is a subscription and an accepted transfer is not.</summary>
	[Fact]
	public void FixedRow_Accepted_OnlyTheCardOneIsSubscription()
	{
		var analysis = ThreeMonths();
		var user = UserData.Empty();
		foreach (var suggestion in analysis.Suggestions.Where(s => s.Amount is 13.99m or 940m))
		{
			user.AcceptFixedPayment(Analysis.ToDefinition(suggestion, new RecurringPaymentId("fixed-" + suggestion.Name), new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero)).Value);
		}

		var rows = new Analysis([.. analysis.Items.Select(i => i.T)], CategoryDefaults.Create(new Dictionary<string, string[]> { ["Housing"] = ["LANDLORD"] }), user, Today).Fixed;

		rows.Where(f => f.IsSubscription).Select(f => f.Def.Amount).Should().Equal(13.99m);
		rows.Should().HaveCount(2);
	}
	#endregion

	#region Rule Tests
	/// <summary>Verifies the cadences that count: monthly and yearly yes, quarterly no.</summary>
	/// <param name="cadenceDays">The rhythm in days.</param>
	/// <param name="expected">Whether it is a subscription.</param>
	[Theory]
	[InlineData(30, true)]
	[InlineData(365, true)]
	[InlineData(91, false)]
	public void IsSubscription_CardPayments_DependsOnCadence(int cadenceDays, bool expected)
	{
		SubscriptionRule.IsSubscription(cadenceDays, [CardPayment()]).Should().Be(expected);
	}

	/// <summary>Verifies that a direct debit is not a subscription, even monthly.</summary>
	[Fact]
	public void IsSubscription_DirectDebit_False()
	{
		var debit = CardPayment() with { Kind = Kind.DirectDebit };

		SubscriptionRule.IsSubscription(30, [debit]).Should().BeFalse();
	}

	/// <summary>Verifies that a series mixing card payments and a transfer is not a subscription.</summary>
	[Fact]
	public void IsSubscription_MixedCardAndTransfer_False()
	{
		var transfer = CardPayment() with { Kind = Kind.Transfer };

		SubscriptionRule.IsSubscription(30, [CardPayment(), transfer]).Should().BeFalse();
	}

	/// <summary>Verifies that no payment at all cannot be told as a subscription.</summary>
	[Fact]
	public void IsSubscription_NoPayments_False() => SubscriptionRule.IsSubscription(30, []).Should().BeFalse();

	/// <summary>Verifies that a missing payment list is refused.</summary>
	[Fact]
	public void IsSubscription_NullPayments_Throws()
	{
		var act = () => SubscriptionRule.IsSubscription(30, null!);

		act.Should().Throw<ArgumentNullException>();
	}
	#endregion
}
