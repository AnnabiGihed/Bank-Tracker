namespace KbcTracker.ViewModels.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds small, invented analyses for the view model tests: payments of an invented account, the default
///              categories plus a Trips category with a Rail trips child, and a fixed "today", so results do not depend on
///              the machine or the day the tests run.
/// </summary>
internal static class TestWorkspace
{
	#region Static Instances
	/// <summary>The fixed "today" of every analysis built here.</summary>
	public static readonly DateOnly Today = new(2026, 11, 9);
	#endregion

	#region Public Methods
	/// <summary>Creates a payment of the invented account, without bank JSON, as older histories hold.</summary>
	/// <param name="id">The transaction id.</param>
	/// <param name="date">The booking date.</param>
	/// <param name="amount">The amount; negative for money out.</param>
	/// <param name="counterparty">The other party's name.</param>
	/// <param name="description">The bank's description.</param>
	/// <returns>The transaction.</returns>
	public static Txn Payment(string id, DateOnly date, decimal amount, string counterparty, string description = "") =>
		Txn.Rehydrate(new TxnId(id), "acc-1", date, Money.Rehydrate(amount, "EUR"), Counterparty.Rehydrate(counterparty, null),
			description.Length == 0 ? counterparty + " BE1000 Bruxelles Payment with card" : description, BankRecord.None);

	/// <summary>Creates the categories: the defaults, and Trips with its child Rail trips.</summary>
	/// <returns>The categories.</returns>
	public static IList<Category> Categories()
	{
		var categories = CategoryDefaults.CreateDefaults("BE");
		categories.Add(Category.Create(new CategoryId("trav"), "Trips", CategoryType.Expense, "#3454d1", [], builtIn: false).Value);
		categories.Add(Category.Create(new CategoryId("trav-train"), "Rail trips", CategoryType.Expense, "#56ccf2", [], builtIn: false, new CategoryId("trav")).Value);
		return categories;
	}

	/// <summary>Analyses payments with the decisions the caller makes.</summary>
	/// <param name="payments">The payments.</param>
	/// <param name="decide">Records the owner's decisions; it sees the analysis of the payments without any, to find series keys.</param>
	/// <returns>The analysis with the decisions applied.</returns>
	public static Analysis Analyse(IEnumerable<Txn> payments, Action<UserData, Analysis>? decide = null)
	{
		var list = payments.ToList();
		var categories = Categories();
		var user = UserData.Empty();
		decide?.Invoke(user, new Analysis(list, categories, UserData.Empty(), Today));
		return new Analysis(list, categories, user, Today);
	}

	/// <summary>Gets the series key of the payee of a payment.</summary>
	/// <param name="analysis">The analysis.</param>
	/// <param name="transactionId">The payment.</param>
	/// <returns>The series key.</returns>
	public static string SeriesKeyOf(Analysis analysis, string transactionId) => analysis.Items.Single(i => i.T.Id.Equals(new TxnId(transactionId))).SeriesKey;

	/// <summary>Creates a connected bank session for display.</summary>
	/// <param name="validUntil">When the bank access ends.</param>
	/// <param name="lastSync">When the bank was last read.</param>
	/// <param name="lastError">The last sync's failure, or <see langword="null"/>.</param>
	/// <param name="skipped">How many transactions the last sync left out.</param>
	/// <returns>The session.</returns>
	public static SessionInfo Session(DateTimeOffset validUntil, DateTimeOffset? lastSync, string? lastError = null, int skipped = 0) =>
		SessionInfo.Rehydrate(new BankSessionId("display-only"), ["0", "1"], DateTimeOffset.MinValue, validUntil, lastSync, lastError, skipped);
	#endregion
}
