namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Pins how <see cref="Analysis"/> uses the owner's own accounts (ADR-0010): a payment to or from one of their
///              IBANs is a bank-certain own transfer, a transfer into a savings account counts as Saved, money coming back
///              is ignored, and without account details only the holder name decides.
///
///              Uses invented IBANs; the holder is <see cref="BankJson.Holder"/>.
/// </summary>
public sealed class AnalysisOwnAccountTests
{
	#region Constants
	/// <summary>The IBAN of the account the card payments are booked on.</summary>
	private const string CurrentIban = "BE00111122223333";

	/// <summary>The IBAN of the second account.</summary>
	private const string OtherIban = "BE00444455556666";

	/// <summary>The Enable Banking id of the second account.</summary>
	private const string OtherUid = "acc-2";
	#endregion

	#region Test Infrastructure
	/// <summary>The fixed "today" of every analysis in this class.</summary>
	private static readonly DateOnly Today = new(2026, 11, 9);

	/// <summary>Runs the analysis over two card payments of the current account (the main one) and the given payments.</summary>
	private static Analysis Analyse(IEnumerable<OwnAccount>? accounts, params Txn[] txns) =>
		new([BankJson.Card("2026-11-01", 5m, "SHOP ONE BE1000 Bruxelles"), BankJson.Card("2026-11-02", 6m, "SHOP TWO BE1000 Bruxelles"), .. txns],
			CategoryDefaults.CreateDefaults("BE"), UserData.Empty(), Today, accounts);

	/// <summary>The two accounts, with the given types.</summary>
	private static OwnAccount[] Accounts(string? currentType, string? otherType) =>
	[
		new OwnAccount(BankJson.AccountUid, " be00 1111 2222 3333", "Current", currentType),
		new OwnAccount(OtherUid, OtherIban, "Second", otherType),
	];

	/// <summary>Moves a payment to another account of the owner.</summary>
	private static Txn Booked(Txn txn, string accountUid) => Txn.Rehydrate(txn.Id, accountUid, txn.Date, Money.Rehydrate(txn.Amount, txn.Currency),
		Counterparty.Rehydrate(txn.Counterparty, txn.CounterpartyIban), txn.Description, new BankRecord(txn.BankCode, txn.Raw));

	/// <summary>Gets the analysed payment of an amount.</summary>
	private static TxnInfo Payment(Analysis analysis, decimal amount) => analysis.Items.Single(i => i.T.Amount == amount);
	#endregion

	#region Own Transfer Tests
	/// <summary>
	/// Verifies that a transfer to another current account of the owner is an own transfer the bank makes certain, whatever
	/// name it carries, and is no spending.
	/// </summary>
	[Fact]
	public void Classify_TransferToOwnCurrentAccount_OwnTransferIgnored()
	{
		var analysis = Analyse(Accounts("CACC", "CACC"), BankJson.TransferOut("2026-11-03", 300m, "SOMEONE ELSE", OtherIban));

		var item = Payment(analysis, -300m);
		item.Kind.Should().Be(Kind.OwnTransferOut);
		item.Category.Id.Value.Should().Be("own-transfers");
		item.CategoryConfirmed.Should().BeTrue();
		analysis.MonthData("2026-11").Spent.Should().Be(11m);
		analysis.MonthData("2026-11").Saved.Should().Be(0m);
	}

	/// <summary>
	/// Verifies that without the accounts the same transfer is plain spending: only the holder name is the fallback.
	/// </summary>
	[Fact]
	public void Classify_WithoutAccounts_TransferToOwnIbanIsSpending()
	{
		var analysis = Analyse(null, BankJson.TransferOut("2026-11-03", 300m, "SOMEONE ELSE", OtherIban));

		Payment(analysis, -300m).Kind.Should().Be(Kind.Transfer);
		analysis.MonthData("2026-11").Spent.Should().Be(311m);
	}

	/// <summary>
	/// Verifies that the holder name still makes an own transfer when the IBAN is not a known account.
	/// </summary>
	[Fact]
	public void Classify_HolderNameOnUnknownIban_StillOwnTransfer()
	{
		var analysis = Analyse(Accounts("CACC", "CACC"), BankJson.TransferOut("2026-11-03", 80m, BankJson.Holder, "BE00999988887777"));

		Payment(analysis, -80m).Kind.Should().Be(Kind.OwnTransferOut);
		Payment(analysis, -80m).Category.Id.Value.Should().Be("own-transfers");
	}

	/// <summary>
	/// Verifies that money coming from another own account is an own transfer in, never income.
	/// </summary>
	[Fact]
	public void Classify_CreditFromOwnAccount_OwnTransferInIgnored()
	{
		var analysis = Analyse(Accounts("CACC", "SVGS"), BankJson.TransferIn("2026-11-03", 500m, "SOMEONE ELSE", "Back", debtorIban: OtherIban));

		var item = Payment(analysis, 500m);
		item.Kind.Should().Be(Kind.OwnTransferIn);
		item.Category.Id.Value.Should().Be("own-transfers");
		analysis.MonthData("2026-11").Income.Should().Be(0m);
	}
	#endregion

	#region Savings Tests
	/// <summary>
	/// Verifies that a transfer into an account the bank types as savings counts as Saved, bank-certain, and as no spending.
	/// </summary>
	[Fact]
	public void Classify_TransferToSavingsAccount_Saved()
	{
		var analysis = Analyse(Accounts("CACC", "SVGS"), BankJson.TransferOut("2026-11-03", 300m, "SAVINGS POT", OtherIban));

		var item = Payment(analysis, -300m);
		item.Category.Id.Value.Should().Be("savings");
		item.IsSaving.Should().BeTrue();
		item.CategoryConfirmed.Should().BeTrue();
		analysis.MonthData("2026-11").Saved.Should().Be(300m);
		analysis.MonthData("2026-11").Spent.Should().Be(11m);
	}

	/// <summary>
	/// Verifies that without an account type nothing is guessed: the transfer stays an ordinary own transfer.
	/// </summary>
	/// <param name="otherType">The type of the target account, missing or a current account.</param>
	[Theory]
	[InlineData(null)]
	[InlineData("CACC")]
	public void Classify_OtherAccountNotTypedSavings_TransferStaysOwnTransfer(string? otherType)
	{
		var analysis = Analyse(Accounts(null, otherType), BankJson.TransferOut("2026-11-03", 300m, "SAVINGS POT", OtherIban));

		var item = Payment(analysis, -300m);
		item.IsSaving.Should().BeFalse();
		item.Category.Id.Value.Should().Be("own-transfers");
		analysis.MonthData("2026-11").Saved.Should().Be(0m);
	}

	/// <summary>
	/// Verifies that a transfer from the savings account back to the current one is ignored, with and without types, and that
	/// the credit side on the current account is ignored too.
	/// </summary>
	/// <param name="currentType">The type of the current account.</param>
	/// <param name="otherType">The type of the second account.</param>
	[Theory]
	[InlineData("CACC", "SVGS")]
	[InlineData(null, "SVGS")]
	[InlineData(null, null)]
	public void Classify_MoneyBackFromSavings_Ignored(string? currentType, string? otherType)
	{
		var back = Booked(BankJson.TransferOut("2026-11-03", 200m, "CURRENT ACCOUNT", CurrentIban), OtherUid);
		var arrival = BankJson.TransferIn("2026-11-03", 200.5m, "SAVINGS POT", "From savings", debtorIban: OtherIban);

		var analysis = Analyse(Accounts(currentType, otherType), back, arrival);

		Payment(analysis, -200m).Category.Id.Value.Should().Be("own-transfers");
		Payment(analysis, 200.5m).Category.Id.Value.Should().Be("own-transfers");
		analysis.MonthData("2026-11").Saved.Should().Be(0m);
	}

	/// <summary>
	/// Verifies that the owner's own choice for the transfer still wins over the Saved rule.
	/// </summary>
	[Fact]
	public void Classify_OwnerCategoryOnTransferToSavings_Wins()
	{
		var transfer = BankJson.TransferOut("2026-11-03", 300m, "SAVINGS POT", OtherIban);
		var user = UserData.Empty();
		user.AssignTransactionCategory(transfer.Id, new CategoryId("other"));

		var analysis = new Analysis([transfer], CategoryDefaults.CreateDefaults("BE"), user, Today, Accounts("CACC", "SVGS"));

		analysis.Items.Single().Category.Id.Value.Should().Be("other");
	}
	#endregion
}
