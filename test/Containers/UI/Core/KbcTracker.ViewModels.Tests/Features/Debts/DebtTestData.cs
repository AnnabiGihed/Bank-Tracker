namespace KbcTracker.ViewModels.Tests.Features.Debts;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds the invented payments and debts the debt view model tests use: a friend who lends money and is
///              repaid by transfers, and a bank loan with instalments. Every name, IBAN and reference is invented. "Today"
///              is <see cref="TestWorkspace.Today"/>.
/// </summary>
internal static class DebtTestData
{
	#region Constants
	/// <summary>The invented friend's IBAN.</summary>
	public const string FriendIban = "BE00111122223333";

	/// <summary>The invented friend's name.</summary>
	public const string Friend = "Friend One";
	#endregion

	#region Public Methods
	/// <summary>Creates money in from the friend, which the owner has put under Borrowed from people.</summary>
	/// <param name="id">The transaction id.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount received.</param>
	/// <returns>The credit.</returns>
	public static Txn FriendCredit(string id, DateOnly date, decimal amount) =>
		Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(amount, "EUR"), Counterparty.Rehydrate(Friend, FriendIban), "Thanks", BankRecord.None);

	/// <summary>Creates a transfer out to the friend.</summary>
	/// <param name="id">The transaction id.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount sent, positive.</param>
	/// <returns>The debit.</returns>
	public static Txn ToFriend(string id, DateOnly date, decimal amount) =>
		Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(-amount, "EUR"), Counterparty.Rehydrate(Friend, FriendIban), "Repayment", BankRecord.None);

	/// <summary>Creates a loan instalment of a bank loan, a <c>CSLN/PPAY</c> debit with the loan's reference.</summary>
	/// <param name="id">The transaction id.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The instalment, positive.</param>
	/// <param name="reference">The loan reference.</param>
	/// <returns>The debit.</returns>
	public static Txn LoanInstalment(string id, DateOnly date, decimal amount, string reference)
	{
		var description = $"Loan repayment Reference {reference} capital and interest";
		var raw = new System.Text.Json.Nodes.JsonObject
		{
			["creditor"] = new System.Text.Json.Nodes.JsonObject { ["name"] = "KBC BANK NV" },
			["debtor"] = new System.Text.Json.Nodes.JsonObject { ["name"] = "JANE DOE" },
			["bank_transaction_code"] = new System.Text.Json.Nodes.JsonObject { ["description"] = "PMNT", ["code"] = "CSLN", ["sub_code"] = "PPAY" },
			["credit_debit_indicator"] = "DBIT",
			["status"] = "BOOK",
			["remittance_information"] = description,
		};
		return Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(-amount, "EUR"), Counterparty.Rehydrate("KBC BANK NV", null), description,
			new BankRecord("PMNT", raw.ToJsonString()));
	}

	/// <summary>Creates the terms of a loan from the friend.</summary>
	/// <param name="plan">How it is repaid, or <see langword="null"/>.</param>
	/// <param name="creditId">The credit that paid it out, or <see langword="null"/>.</param>
	/// <param name="borrowedOn">The day it was borrowed.</param>
	/// <param name="principal">The amount received.</param>
	/// <param name="lender">The lender's name.</param>
	/// <returns>The terms.</returns>
	public static DebtTerms FriendTerms(RepaymentPlan? plan, string? creditId = "c1", DateOnly? borrowedOn = null, decimal principal = 300m, string lender = Friend) =>
		new(DebtKind.PersonalLoan, lender, FriendIban, creditId is null ? null : new TxnId(creditId), principal, null, plan, borrowedOn ?? new DateOnly(2026, 10, 5));

	/// <summary>Creates a monthly plan of instalments.</summary>
	/// <param name="amount">The total to repay.</param>
	/// <param name="start">The first date.</param>
	/// <param name="each">The amount of each instalment.</param>
	/// <returns>The plan.</returns>
	public static RepaymentPlan Monthly(decimal amount, DateOnly start, decimal each) =>
		RepaymentPlan.Instalments(amount, start, RepaymentFrequency.Monthly, null, each, null).Value;

	/// <summary>Analyses payments with the friend's credit put under Borrowed from people and the given debts added.</summary>
	/// <param name="payments">The payments.</param>
	/// <param name="debts">The debts, with their ids.</param>
	/// <returns>The analysis.</returns>
	public static Analysis Analyse(IEnumerable<Txn> payments, params (string Id, DebtTerms Terms)[] debts) =>
		TestWorkspace.Analyse(payments, (user, _) =>
		{
			foreach (var credit in payments.Where(p => p.Amount > 0))
			{
				user.AssignTransactionCategory(credit.Id, CategoryId.BorrowedPeople);
			}

			foreach (var (id, terms) in debts)
			{
				user.AddDebt(new DebtId(id), terms).IsSuccess.Should().BeTrue();
			}
		});
	#endregion
}
