namespace KbcTracker.ViewModels.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Builds the <see cref="DecisionRequest"/> of every user action with exactly the fields the backend's
///              decisions endpoint reads, so no component spells an action name or a field itself.
/// </summary>
public static class DecisionRequests
{
	#region Public Methods
	/// <summary>Confirms the automatic category of one payee.</summary>
	/// <param name="seriesKey">The payee's series key.</param>
	/// <returns>The <c>confirm</c> request.</returns>
	public static DecisionRequest Confirm(string seriesKey) => new("confirm", Key: seriesKey);

	/// <summary>Confirms the automatic category of several payees together.</summary>
	/// <param name="seriesKeys">The payees' series keys.</param>
	/// <returns>The <c>confirm</c> request.</returns>
	public static DecisionRequest ConfirmMany(IEnumerable<string> seriesKeys)
	{
		ArgumentNullException.ThrowIfNull(seriesKeys);
		return new("confirm", Keys: [.. seriesKeys]);
	}

	/// <summary>Sets the category of a payee, for all its payments past and future.</summary>
	/// <param name="seriesKey">The payee's series key.</param>
	/// <param name="categoryId">The chosen category.</param>
	/// <returns>The <c>payeeCategory</c> request; its answer carries the assignment an undo restores.</returns>
	public static DecisionRequest PayeeCategory(string seriesKey, string categoryId) => new("payeeCategory", Key: seriesKey, CategoryId: categoryId);

	/// <summary>Sets the category of one payment only, and restores the payee's assignment from before the change.</summary>
	/// <param name="seriesKey">The payee's series key.</param>
	/// <param name="transactionId">The payment.</param>
	/// <param name="categoryId">The chosen category.</param>
	/// <param name="previous">The payee's assignment before the change, from the answer to <see cref="PayeeCategory"/>.</param>
	/// <returns>The <c>txnOnly</c> request.</returns>
	public static DecisionRequest TransactionOnly(string seriesKey, string transactionId, string categoryId, PayeeAssignmentSnapshot? previous) =>
		new("txnOnly", Key: seriesKey, Id: transactionId, CategoryId: categoryId, Restore: previous);

	/// <summary>Accepts a suggested fixed payment.</summary>
	/// <param name="signature">The suggestion's signature.</param>
	/// <returns>The <c>accept</c> request.</returns>
	public static DecisionRequest Accept(string signature) => new("accept", Key: signature);

	/// <summary>Dismisses a suggested fixed payment.</summary>
	/// <param name="signature">The suggestion's signature.</param>
	/// <returns>The <c>dismiss</c> request.</returns>
	public static DecisionRequest Dismiss(string signature) => new("dismiss", Key: signature);

	/// <summary>Marks a payee as a fixed payment.</summary>
	/// <param name="seriesKey">The payee's series key.</param>
	/// <returns>The <c>mark</c> request.</returns>
	public static DecisionRequest Mark(string seriesKey) => new("mark", Key: seriesKey);

	/// <summary>Stops tracking a fixed payment.</summary>
	/// <param name="fixedPaymentId">The fixed payment.</param>
	/// <returns>The <c>removeFixed</c> request.</returns>
	public static DecisionRequest RemoveFixed(string fixedPaymentId) => new("removeFixed", Id: fixedPaymentId);

	/// <summary>Changes a fixed payment.</summary>
	/// <param name="fixedPaymentId">The fixed payment.</param>
	/// <param name="name">The new name.</param>
	/// <param name="categoryId">The new category.</param>
	/// <param name="amount">The new usual amount.</param>
	/// <param name="cadenceDays">The new rhythm in days.</param>
	/// <returns>The <c>updateFixed</c> request.</returns>
	public static DecisionRequest UpdateFixed(string fixedPaymentId, string name, string categoryId, decimal amount, int cadenceDays) =>
		new("updateFixed", Id: fixedPaymentId, Name: name, CategoryId: categoryId, Amount: amount, Cadence: cadenceDays);

	/// <summary>Divides a payment into categorised parts.</summary>
	/// <param name="transactionId">The payment.</param>
	/// <param name="parts">The parts, as positive amounts that add up to the payment.</param>
	/// <returns>The <c>split</c> request.</returns>
	public static DecisionRequest Split(string transactionId, IReadOnlyList<SplitPartRequest> parts) => new("split", Id: transactionId, Parts: parts);

	/// <summary>Removes the split of a payment.</summary>
	/// <param name="transactionId">The payment.</param>
	/// <returns>The <c>unsplit</c> request.</returns>
	public static DecisionRequest Unsplit(string transactionId) => new("unsplit", Id: transactionId);

	/// <summary>Renames a payee.</summary>
	/// <param name="seriesKey">The payee's series key.</param>
	/// <param name="name">The new name, or <see langword="null"/> to go back to the automatic one.</param>
	/// <returns>The <c>rename</c> request.</returns>
	public static DecisionRequest Rename(string seriesKey, string? name) => new("rename", Key: seriesKey, Name: name);

	/// <summary>Creates or changes a category.</summary>
	/// <param name="id">The category, or <see langword="null"/> for a new one.</param>
	/// <param name="name">The name.</param>
	/// <param name="type">The type name; empty for a sub-category, which follows its root, and the category's own type for a built-in one.</param>
	/// <param name="parentId">The parent: empty for top level, <see langword="null"/> to leave a built-in category where it is.</param>
	/// <param name="color">The colour as <c>#rrggbb</c>.</param>
	/// <param name="keywords">The matching keywords, comma separated.</param>
	/// <returns>The <c>saveCategory</c> request; its answer carries the category's id.</returns>
	public static DecisionRequest SaveCategory(string? id, string name, string type, string? parentId, string color, string keywords) =>
		new("saveCategory", Id: id, Name: name, Type: type, ParentId: parentId, Color: color, Keywords: keywords);

	/// <summary>Deletes a category; its payments move to its parent.</summary>
	/// <param name="categoryId">The category.</param>
	/// <returns>The <c>deleteCategory</c> request.</returns>
	public static DecisionRequest DeleteCategory(string categoryId) => new("deleteCategory", Id: categoryId);
	#endregion
}
