namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds domain objects as they come from the JSON data folder, with values that are easy to get
///              wrong in a database: a non-UTC time, an amount with trailing zeros, a keyword with a trailing space.
/// </summary>
internal static class Samples
{
	#region Static Instances
	/// <summary>
	/// A time with a +01:00 offset, which Npgsql refuses unless it is converted to UTC.
	/// </summary>
	public static readonly DateTimeOffset BrusselsMorning = new(2026, 11, 9, 9, 30, 0, TimeSpan.FromHours(1));
	#endregion

	#region Public Methods
	/// <summary>
	/// A card payment with its raw bank JSON.
	/// </summary>
	/// <param name="id">The transaction id.</param>
	/// <returns>The transaction.</returns>
	public static Txn Txn(string id) => Domain.Features.Transactions.Aggregates.Txn.Rehydrate(
		new TxnId(id),
		"acc-1",
		new DateOnly(2026, 11, 2),
		Money.Rehydrate(-12.50m, "EUR"),
		Counterparty.Rehydrate("Shop One", "BE00111122223333"),
		"SHOP ONE BE1000 Bruxelles Payment with card",
		new BankRecord("PMNT", """{"entry_reference":"x1",  "amount":"12.50"}"""));

	/// <summary>
	/// The groceries category, whose "AD " keyword must keep its trailing space.
	/// </summary>
	/// <returns>The category.</returns>
	public static Category Groceries() =>
		Category.Rehydrate(new CategoryId("groceries"), "Groceries", CategoryType.Expense, "#3454d1", ["AD ", "DELHAIZE"], builtIn: false);

	/// <summary>
	/// A bank connection with two accounts.
	/// </summary>
	/// <returns>The connection.</returns>
	public static SessionInfo Session() =>
		SessionInfo.Rehydrate(new BankSessionId("session-1"), ["current", "savings"], BrusselsMorning, BrusselsMorning.AddDays(180), BrusselsMorning, lastError: null, skippedTransactions: 2);

	/// <summary>
	/// Decisions with one accepted fixed payment and one of each kind of choice.
	/// </summary>
	/// <returns>The decisions.</returns>
	public static UserData Decisions()
	{
		var rent = RecurringPayment.Rehydrate(
			new RecurringPaymentId("rent"),
			"Rent",
			new CategoryId("housing"),
			new PayeeMatch(["TO:BE00111122223333"], ["LANDLORD"]),
			new RecurrenceRule(940.00m, 15m, 30),
			active: true,
			BrusselsMorning);
		return UserData.Rehydrate(
			[rent],
			["CARD:OLD SHOP"],
			[KeyValuePair.Create("CARD:SHOP ONE", new PayeeAssignment(new CategoryId("groceries"), Confirmed: true))],
			[KeyValuePair.Create(new TxnId("t1"), new CategoryId("car"))],
			[KeyValuePair.Create("CARD:SHOP ONE", "Shop One")]);
	}
	#endregion
}
