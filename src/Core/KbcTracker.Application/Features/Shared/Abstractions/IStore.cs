namespace KbcTracker.Application.Features.Shared.Abstractions;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Persistence contract for everything the tracker keeps: the bank session, the transaction history,
///              the user's decisions and the category definitions.
///
///              Exists so the domain code (sync, analysis) does not depend on where data lives: the web host
///              implements it with JSON files today, and the Android app will implement it with SQLite.
///              Implementations must never delete transactions, because the bank only returns about 90 days.
/// </summary>
public interface IStore
{
	#region Methods
	/// <summary>
	/// Loads the current bank session.
	/// </summary>
	/// <returns>The session, or <see langword="null"/> when the user has not connected to the bank yet.</returns>
	SessionInfo? LoadSession();

	/// <summary>
	/// Saves the bank session, replacing the previous one.
	/// </summary>
	/// <param name="session">The session to save.</param>
	void SaveSession(SessionInfo session);

	/// <summary>
	/// Loads the full transaction history of every account.
	/// </summary>
	/// <returns>All stored transactions; an empty list when none are stored.</returns>
	IReadOnlyList<Txn> LoadTransactions();

	/// <summary>
	/// Adds new transactions and updates known ones, matched by their <see cref="TxnId"/>. Never deletes.
	/// </summary>
	/// <param name="incoming">The transactions downloaded from the bank.</param>
	/// <returns>The number of transactions that were not stored before.</returns>
	int Merge(IEnumerable<Txn> incoming);

	/// <summary>
	/// Loads everything the user decided in the UI.
	/// </summary>
	/// <returns>The user's decisions; an empty <see cref="UserData"/> when nothing was decided yet.</returns>
	UserData LoadUserData();

	/// <summary>
	/// Saves everything the user decided in the UI, replacing the previous version.
	/// </summary>
	/// <param name="data">The user's decisions.</param>
	void SaveUserData(UserData data);

	/// <summary>
	/// Loads the category definitions, creating the default ones on first use.
	/// </summary>
	/// <param name="seedKeywords">Category names with their keywords, used only when no categories exist yet.</param>
	/// <returns>The category definitions, as a list the caller may edit and pass to <see cref="SaveCategories"/>.</returns>
	IList<Category> LoadCategories(IReadOnlyDictionary<string, string[]> seedKeywords);

	/// <summary>
	/// Saves the category definitions, replacing the previous ones.
	/// </summary>
	/// <param name="categories">The category definitions.</param>
	void SaveCategories(IEnumerable<Category> categories);
	#endregion
}
