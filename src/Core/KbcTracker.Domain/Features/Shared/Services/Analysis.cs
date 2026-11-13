using System.Globalization;
using System.Text.RegularExpressions;

namespace KbcTracker.Domain.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The brain of the tracker: classifies every transaction by its ISO bank transaction code, groups
///              payments into payee series, resolves categories, matches accepted fixed payments, suggests new ones,
///              and builds the month summaries and the review queue.
///
///              Immutable once built: every page or sync builds a new one from the stored data. Categories resolve
///              in this order: the transaction's own > the payee's > the fixed payment's > automatic (bank code or
///              keyword). Fixed payments are only ever suggested, never accepted on the user's behalf.
///
///              A split payment (ADR-0007) keeps that category for payee logic, but its money is carried by its
///              parts, which every total by category adds up.
/// </summary>
public sealed partial class Analysis
{
	#region Constants
	/// <summary>
	/// The name of the built-in category for moves between the user's own accounts.
	/// </summary>
	public const string OwnTransfersName = "Own transfers";

	/// <summary>
	/// The name of the built-in savings category.
	/// </summary>
	public const string SavingsName = "Savings & investments";

	/// <summary>
	/// The longest a regular expression may run on one bank label before it is abandoned.
	/// </summary>
	private const int RegexTimeoutMilliseconds = 1000;
	#endregion

	#region Static Instances
	/// <summary>
	/// Words too common in payee and shop names to identify one: generic words, legal forms and city names.
	/// </summary>
	private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
	{
		"THE", "AND", "ONLINE", "SHOP", "STORE", "PAYMENT", "BELGIUM", "BELGIQUE", "BELGIE", "BRUXELLES", "BRUSSEL", "BRUSSELS", "BRUX",
		"PARIS", "DUBLIN", "LUXEMBOURG", "AMSTERDAM", "LONDON", "IXELLES", "ELSENE", "ANDERLECHT", "ETTERBEEK", "SCHAERBEEK",
		"SPRL", "SRL", "BVBA", "SAS", "SARL", "LTD", "INC", "GMBH", "COM", "WWW", "NET", "SERVICES", "SERVICE",
	};

	/// <summary>
	/// Payment-processor prefixes that precede the shop name on card labels (for example <c>WL*</c>, <c>SUMUP *</c>).
	/// </summary>
	private static readonly HashSet<string> ProcessorPrefixes = new(StringComparer.Ordinal)
	{
		"WL", "SQ", "SUMUP", "SP", "ZETTLE", "ZTL", "IZ", "PP", "PAYPAL", "UBR", "CCV", "MOLLIE",
	};
	#endregion

	#region Fields
	private readonly Dictionary<string, Category> _categories;
	private readonly DateOnly _today;
	private readonly CategoryKeywordMatcher _matcher;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="Analysis"/> by classifying every transaction, resolving categories, matching
	/// accepted fixed payments and computing suggestions, month summaries and the review queue.
	/// </summary>
	/// <param name="txns">The full transaction history, of every account.</param>
	/// <param name="categories">The category definitions; should contain the built-in <c>other</c> category.</param>
	/// <param name="user">Everything the user decided: fixed payments, payee categories, dismissals and names.</param>
	/// <param name="today">The reference date for "late" and "still active" checks; defaults to the local date.
	/// Tests pass a fixed date so results do not depend on when they run.</param>
	public Analysis(IEnumerable<Txn> txns, IEnumerable<Category> categories, UserData user, DateOnly? today = null)
	{
		_today = today ?? DateOnly.FromDateTime(DateTime.Today);
		CategoryList = [.. categories];
		_categories = CategoryList.ToDictionary(c => c.Id.Value, StringComparer.Ordinal);
		Tree = new CategoryTree(CategoryList);
		_matcher = new CategoryKeywordMatcher(CategoryList, Tree);

		var parsed = txns.Select(t => (Txn: t, Fields: BankFields.From(t))).ToList();
		HolderName = parsed.Where(p => p.Txn.Amount < 0).Select(p => p.Fields.DebtorName).OfType<string>()
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.GroupBy(Norm, StringComparer.Ordinal).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "";

		var active = user.Recurring.Where(r => r.Active).ToList();
		Items = parsed.Select(p => Understand(p.Txn, p.Fields, user, active))
			.OrderByDescending(i => i.T.Date).ThenBy(i => i.T.Amount).ToList();

		Fixed = active.Select(r => new FixedRow(
				r,
				Cat(r.CategoryId?.Value ?? Items.FirstOrDefault(i => i.Recurring == r)?.Category.Id.Value),
				Items.Where(i => i.Recurring == r).OrderBy(i => i.T.Date).ToList()))
			.OrderBy(f => f.UsualDay).ToList();
		Suggestions = Suggest(user);
		Review = BuildReview();

		var months = Items.Select(i => i.Month).Distinct(StringComparer.Ordinal).OrderDescending(StringComparer.Ordinal).ToList();
		if (!months.Contains(MonthOf(_today), StringComparer.Ordinal))
		{
			months.Insert(0, MonthOf(_today));
		}

		AvailableMonths = months;
		Months = AvailableMonths.Order(StringComparer.Ordinal).Select(BuildMonth).ToList();
	}

	/// <summary>Initialises a display analysis from an already classified backend state.</summary>
	/// <param name="state">The backend's computed display state.</param>
	private Analysis(AnalysisState state)
	{
		_today = state.Today;
		Items = state.Items;
		CategoryList = state.Categories;
		_categories = state.Categories.ToDictionary(c => c.Id.Value, StringComparer.Ordinal);
		Tree = new CategoryTree(state.Categories);
		_matcher = new CategoryKeywordMatcher(state.Categories, Tree);
		Fixed = state.Fixed;
		Suggestions = state.Suggestions;
		Review = state.Review;
		Months = state.Months;
		AvailableMonths = state.AvailableMonths.ToList();
		HolderName = state.HolderName;
	}
	#endregion

	#region Factory Methods
	/// <summary>Restores an already computed display analysis without reading private bank source records.</summary>
	/// <param name="state">The computed state supplied by the central backend.</param>
	/// <returns>The display analysis.</returns>
	/// <exception cref="ArgumentNullException">The state is null.</exception>
	public static Analysis Rehydrate(AnalysisState state)
	{
		ArgumentNullException.ThrowIfNull(state);
		return new Analysis(state);
	}
	#endregion

	#region Properties
	/// <summary>
	/// Gets every transaction as understood by the analysis, newest first.
	/// </summary>
	public IReadOnlyList<TxnInfo> Items { get; }

	/// <summary>
	/// Gets the active accepted fixed payments with their matched payments, by usual day of the month.
	/// </summary>
	public IReadOnlyList<FixedRow> Fixed { get; }

	/// <summary>
	/// Gets the possible fixed payments the user has not accepted or dismissed, the most certain and largest first.
	/// </summary>
	public IReadOnlyList<Suggestion> Suggestions { get; }

	/// <summary>
	/// Gets the payees whose automatic category the user has not confirmed, largest total first.
	/// </summary>
	public IReadOnlyList<ReviewItem> Review { get; }

	/// <summary>
	/// Gets the summary of every available month, oldest first.
	/// </summary>
	public IReadOnlyList<MonthSummary> Months { get; }

	/// <summary>
	/// Gets the months with transactions plus the current month, as <c>yyyy-MM</c>, newest first.
	/// </summary>
	public IList<string> AvailableMonths { get; }

	/// <summary>
	/// Gets the category definitions the analysis used.
	/// </summary>
	public IReadOnlyList<Category> CategoryList { get; }

	/// <summary>
	/// Gets the category tree built from <see cref="CategoryList"/>. <see cref="MonthSummary.ByCategory"/> holds the
	/// amount of each category itself; the totals through the tree come from <see cref="CategoryTree.RollUp"/>.
	/// </summary>
	public CategoryTree Tree { get; }

	/// <summary>
	/// Gets the account holder's normalised name: the most common debtor of money out. Transfers to or from it are
	/// own transfers. Empty when there is no money out.
	/// </summary>
	public string HolderName { get; }
	#endregion

	#region Public Methods
	/// <summary>
	/// Gets the summary of a month.
	/// </summary>
	/// <param name="month">The month, as <c>yyyy-MM</c>.</param>
	/// <returns>Its summary, or an all-zero summary when the month is not available.</returns>
	public MonthSummary MonthData(string month) =>
		Months.FirstOrDefault(m => string.Equals(m.Month, month, StringComparison.Ordinal))
		?? new(month, 0, 0, 0, 0, new Dictionary<string, decimal>(StringComparer.Ordinal));

	/// <summary>
	/// Gets a category by id.
	/// </summary>
	/// <param name="id">The category id.</param>
	/// <returns>The category, or the <c>other</c> category when the id is unknown.</returns>
	/// <exception cref="KeyNotFoundException">The id is unknown and there is no <c>other</c> category.</exception>
	public Category CategoryById(string id) => _categories.GetValueOrDefault(id) ?? _categories[CategoryId.Other.Value];

	/// <summary>
	/// Turns a suggestion, or a payee the user marked as fixed, into a fixed payment definition. The tolerance for
	/// matching by shop words depends on the kind: wide for bills and fees, narrow for card payments.
	/// </summary>
	/// <param name="suggestion">The suggestion.</param>
	/// <param name="id">The new fixed payment's identifier.</param>
	/// <param name="createdAt">When the user accepted it.</param>
	/// <returns>A new, active fixed payment, or the error that prevents creating it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="suggestion"/> is <see langword="null"/>.</exception>
	public static Result<RecurringPayment> ToDefinition(Suggestion suggestion, RecurringPaymentId id, DateTimeOffset createdAt)
	{
		ArgumentNullException.ThrowIfNull(suggestion);
		var tolerancePct = suggestion.Kind switch { Kind.DirectDebit or Kind.BankFee => 60, Kind.Card => 10, _ => 15 };
		return RecurringPayment.Create(
			id,
			suggestion.Name,
			new CategoryId(suggestion.CategoryId),
			new PayeeMatch(suggestion.SeriesKeys, suggestion.Tokens),
			new RecurrenceRule(suggestion.Amount, tolerancePct, suggestion.CadenceDays),
			createdAt);
	}

	/// <summary>
	/// Builds a suggestion-like group from one payee's money out, for "mark as fixed" on a transaction. Uses the
	/// regular suggestion rules when they apply; otherwise a monthly group at the median amount.
	/// </summary>
	/// <param name="seriesKey">The payee series.</param>
	/// <returns>The group.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The payee has no money out.</exception>
	public Suggestion GroupFor(string seriesKey)
	{
		var items = Items.Where(i => string.Equals(i.SeriesKey, seriesKey, StringComparison.Ordinal) && i.T.Amount < 0)
			.OrderBy(i => i.T.Date).ToList();
		var latest = items[^1];
		return Evaluate(items, [seriesKey], seriesKey) ?? new Suggestion(seriesKey, latest.Name, latest.Category.Id.Value,
			items.Select(i => -i.T.Amount).Order().ElementAt(items.Count / 2), 30, false, "marked by you",
			items, [seriesKey], latest.Kind is Kind.Card ? [.. latest.Tokens] : [], latest.Kind);
	}

	/// <summary>
	/// Names a cadence.
	/// </summary>
	/// <param name="days">The number of days between payments.</param>
	/// <returns>Monthly up to 35 days, Quarterly up to 100, otherwise Yearly.</returns>
	public static string CadenceName(int days) => days switch { <= 35 => "Monthly", <= 100 => "Quarterly", _ => "Yearly" };

	/// <summary>
	/// Formats the month of a date as <c>yyyy-MM</c>, the key used for months everywhere.
	/// </summary>
	/// <param name="date">The date.</param>
	/// <returns>The month key.</returns>
	public static string MonthOf(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
	#endregion

	#region Classification
	/// <summary>
	/// Classifies one transaction and resolves its fixed payment and category.
	/// </summary>
	private TxnInfo Understand(Txn txn, BankFields fields, UserData user, List<RecurringPayment> active)
	{
		var kind = DetectKind(txn, fields);
		var (key, name) = Identify(txn, fields, kind);
		var tokens = Tokens(name);
		var automatic = AutomaticCategory(txn, kind, name);
		if (user.PayeeNames.TryGetValue(key, out var chosenName))
		{
			name = chosenName;
		}

		var recurring = txn.Amount < 0 ? Match(active, key, tokens, -txn.Amount) : null;

		string categoryId;
		bool confirmed;
		if (user.TxnCategories.TryGetValue(txn.Id, out var own))
		{
			(categoryId, confirmed) = (own.Value, true);
		}
		else if (user.Payees.TryGetValue(key, out var payee) && payee.CategoryId is not null)
		{
			(categoryId, confirmed) = (payee.CategoryId.Value, payee.Confirmed);
		}
		else if (recurring?.CategoryId is not null)
		{
			(categoryId, confirmed) = (recurring.CategoryId.Value, true);
		}
		else
		{
			// Categories the bank code makes certain (loan, own transfer, fee…) need no confirmation.
			(categoryId, confirmed) = (automatic, IsBankCertain(kind));
		}

		var category = Cat(categoryId);
		var parts = SplitParts(txn, category, user);
		return new TxnInfo(txn, kind, key, recurring?.Name ?? name, tokens, category, confirmed || parts is not null, recurring, parts);
	}

	/// <summary>
	/// Turns the user's split of a payment into parts signed like the payment. A split that no longer adds up to the
	/// payment gets one more "Difference" part on the payment's own category (or <c>other</c> when that is no longer a
	/// spending category), so totals always match the bank.
	/// </summary>
	/// <returns>The parts, or <see langword="null"/> when the payment is not split.</returns>
	private List<TxnPart>? SplitParts(Txn txn, Category category, UserData user)
	{
		if (!user.Splits.TryGetValue(txn.Id, out var split))
		{
			return null;
		}

		var sign = Math.Sign(txn.Amount);
		var parts = split.Lines.Select(l => new TxnPart(Cat(l.CategoryId.Value), l.Amount * sign, l.Label)).ToList();
		var gap = txn.Amount - parts.Sum(p => p.Amount);
		if (gap != 0)
		{
			parts.Add(new TxnPart(category.Type == CategoryType.Expense ? category : Cat(CategoryId.Other.Value), gap, "Difference"));
		}

		return parts;
	}

	/// <summary>
	/// Determines the kind from the ISO bank transaction code and, for transfers, the account holder's name. Without
	/// a code (old data), card payments and direct debits are recognised from the description.
	/// </summary>
	private Kind DetectKind(Txn txn, BankFields fields)
	{
		var debit = txn.Amount < 0;
		var otherName = debit ? fields.CreditorName : fields.DebtorName;
		var own = HolderName.Length > 0 && otherName is not null && string.Equals(Norm(otherName), HolderName, StringComparison.Ordinal);
		var kind = (fields.Code, fields.SubCode, debit) switch
		{
			("CSLN", _, true) => Kind.Loan,
			("IDDT", _, true) => Kind.DirectDebit,
			("SETT", "SUBS", true) => Kind.SavingsPlan,
			("OPCL", _, true) or ("ACMT", _, true) => Kind.BankFee,
			("ICCN", _, true) when txn.Description.Contains("spare change", StringComparison.OrdinalIgnoreCase) => Kind.SpareChange,
			("CCRD", "CWDL", true) => Kind.CashWithdrawal,
			("CCRD", "CDPT", false) => Kind.CashDeposit,
			("CCRD", _, true) => Kind.Card,
			("CCRD", _, false) => Kind.Refund,
			("RCDT", "CAJT", false) => Kind.LoanReceived,
			(_, _, true) when own => Kind.OwnTransferOut,
			(_, _, false) when own => Kind.OwnTransferIn,
			(_, _, true) when fields.Code is "ICDT" or "IRCT" or "ICCN" or "RCDT" or "RRCT" => Kind.Transfer,
			(_, _, false) => Kind.Income,
			_ => Kind.Other,
		};
		if (fields.Code is null && debit)
		{
			if (txn.Description.Contains("Direct debit", StringComparison.OrdinalIgnoreCase) || txn.Description.Contains("Domicili", StringComparison.OrdinalIgnoreCase))
			{
				kind = Kind.DirectDebit;
			}
			else if (PaymentWith().IsMatch(txn.Description))
			{
				kind = Kind.Card;
			}
		}

		return kind;
	}

	/// <summary>
	/// Builds the payee series key and display name. Card payments are grouped by shop name, direct debits by
	/// creditor and mandate, loans by reference, transfers by IBAN.
	/// </summary>
	private static (string Key, string Name) Identify(Txn txn, BankFields fields, Kind kind)
	{
		var debit = txn.Amount < 0;
		var otherName = debit ? fields.CreditorName : fields.DebtorName;
		var description = txn.Description;
		switch (kind)
		{
			case Kind.Card or Kind.Refund:
			{
				var shop = Merchant(description.Length > 0 ? description : txn.Counterparty);
				return ("CARD:" + string.Join(' ', Tokens(shop).Take(3).DefaultIfEmpty(Norm(shop))), shop);
			}

			case Kind.DirectDebit:
			{
				var creditor = Title(otherName ?? txn.Counterparty);
				var mandate = Mandate().Match(description);
				return ("DD:" + Norm(creditor) + (mandate.Success ? ":" + mandate.Groups["mandate"].Value : ""), creditor);
			}

			case Kind.Loan:
			{
				var reference = LoanReference().Match(description);
				return ("LOAN:" + (reference.Success ? reference.Groups["reference"].Value : "1"), "Loan repayment");
			}

			case Kind.SavingsPlan:
			{
				var plan = Title(Before(description, " Subscription"));
				return ("PLAN:" + Norm(plan), plan);
			}

			case Kind.BankFee:
			{
				var fee = Title(Before(Before(description, " Period"), " Periode").Replace("Charge ", "", StringComparison.OrdinalIgnoreCase)) + " (fee)";
				return ("FEE:" + Norm(fee), fee);
			}

			case Kind.SpareChange:
				return ("SPARE", "Spare change investing");
			case Kind.CashWithdrawal:
				return ("CASHOUT", "Cash withdrawal");
			case Kind.CashDeposit:
				return ("CASHIN", "Cash deposit");
			case Kind.LoanReceived:
				return ("LOANIN", "Loan received");
			default:
			{
				var payee = Title(otherName ?? txn.Counterparty);
				var otherIban = debit ? fields.CreditorIban : fields.DebtorIban;
				return ((debit ? "TO:" : "FROM:") + (otherIban ?? Norm(payee)), payee);
			}
		}
	}

	/// <summary>
	/// Chooses the automatic category: from the kind when the bank code makes it certain, otherwise by keyword.
	/// For card payments only the shop name is searched, because card descriptions contain words such as
	/// "Google Pay" or "KBC" that would match unrelated keywords.
	/// </summary>
	private string AutomaticCategory(Txn txn, Kind kind, string name) => kind switch
	{
		Kind.Loan => "loan",
		Kind.SavingsPlan or Kind.SpareChange => "savings",
		Kind.BankFee => "bank-fees",
		Kind.OwnTransferOut or Kind.OwnTransferIn or Kind.CashDeposit => "own-transfers",
		Kind.LoanReceived => CategoryId.LoanReceived.Value,
		Kind.CashWithdrawal => "cash",
		Kind.Income => KeywordCategory(name, CategoryType.Income) ?? "income",
		Kind.Refund => "refunds",
		_ => KeywordCategory(name, null) ?? (kind == Kind.Card ? null : KeywordCategory(txn.Description, null))
			 ?? kind switch { Kind.DirectDebit => "bills", Kind.Transfer => "transfers", _ => "other" },
	};

	/// <summary>
	/// Determines whether the bank code makes the category certain, so the user need not confirm it.
	/// </summary>
	private static bool IsBankCertain(Kind kind) => kind is Kind.Loan or Kind.SavingsPlan or Kind.SpareChange or Kind.BankFee
		or Kind.OwnTransferOut or Kind.OwnTransferIn or Kind.LoanReceived or Kind.CashDeposit or Kind.CashWithdrawal;

	/// <summary>
	/// Finds the accepted fixed payment a transaction belongs to: by payee series first, then by all its shop words
	/// with the amount within the payment's tolerance (at least 0.50 €).
	/// </summary>
	private static RecurringPayment? Match(List<RecurringPayment> definitions, string key, string[] tokens, decimal amount) =>
		definitions.Find(d => d.SeriesKeys.Contains(key, StringComparer.Ordinal))
		?? definitions.Find(d => d.Tokens.Count > 0
			&& d.Tokens.All(token => tokens.Contains(token, StringComparer.Ordinal))
			&& Math.Abs(amount - d.Amount) <= Math.Max(0.5m, d.Amount * d.TolerancePct / 100));

	/// <summary>
	/// Finds the category of the longest keyword that starts a word of <paramref name="text"/> (see
	/// <see cref="CategoryKeywordMatcher"/>).
	/// </summary>
	private string? KeywordCategory(string text, CategoryType? only) => _matcher.Find(text, only);

	/// <summary>
	/// Gets a category by id, falling back to <c>other</c> (or a stand-in when even that is missing).
	/// </summary>
	private Category Cat(string? id) =>
		id is not null && _categories.TryGetValue(id, out var category) ? category
		: _categories.GetValueOrDefault(CategoryId.Other.Value)
			?? Category.Rehydrate(CategoryId.Other, "Other", CategoryType.Expense, Category.DefaultColor, [], builtIn: true);
	#endregion

	#region Suggestions
	/// <summary>
	/// Groups payments that are probably the same thing (same payee series, or same amount and a shared
	/// distinctive shop word, e.g. three different "Google YouTube" labels), then keeps groups that come back
	/// on a regular rhythm (monthly / quarterly / yearly) with a stable amount.
	/// </summary>
	private List<Suggestion> Suggest(UserData user)
	{
		var pool = Items.Where(i => i.T.Amount < 0 && i.Recurring is null
									&& i.Kind is not (Kind.OwnTransferOut or Kind.SpareChange or Kind.CashWithdrawal)
									&& i.Category.Type != CategoryType.Transfer).ToList();
		var groups = new UnionFind(pool.Count);
		LinkSameSeries(pool, groups);
		LinkSimilarPayments(pool, groups);

		var result = new List<Suggestion>();
		var clusters = pool.Select((t, i) => (t, i)).GroupBy(x => groups.Find(x.i))
			.Select(g => g.Select(x => x.t).OrderBy(t => t.T.Date).ToList());
		foreach (var cluster in clusters)
		{
			var keys = cluster.Select(c => c.SeriesKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
			var signature = PayeeMatch.SignatureOf(keys);
			if (IsDismissed(user, keys, signature))
			{
				continue;
			}

			var suggestion = Evaluate(cluster, keys, signature);
			if (suggestion is not null)
			{
				result.Add(suggestion);
			}
		}

		return result.OrderByDescending(s => s.HighConfidence).ThenByDescending(s => s.Amount).ToList();
	}

	/// <summary>
	/// Determines whether the user dismissed a suggestion. Signatures stored before November 2026 were sorted with
	/// the current culture instead of ordinally, so that form is accepted too; it differs only for accented or
	/// mixed-case keys.
	/// </summary>
	private static bool IsDismissed(UserData user, List<string> keys, string signature) =>
		user.DismissedSuggestions.Contains(signature)
		|| user.DismissedSuggestions.Contains(string.Join('|', keys.Order(StringComparer.CurrentCulture)));

	/// <summary>
	/// Puts payments of the same payee series in one group.
	/// </summary>
	private static void LinkSameSeries(List<TxnInfo> pool, UnionFind groups)
	{
		foreach (var series in pool.Select((t, i) => (t, i)).GroupBy(x => x.t.SeriesKey, StringComparer.Ordinal))
		{
			var first = series.First().i;
			foreach (var other in series.Skip(1))
			{
				groups.Union(first, other.i);
			}
		}
	}

	/// <summary>
	/// Links payments of different series with a similar amount (±3 %) and a distinctive shop word (at least five
	/// letters) in common. Contracts (direct debits, loans, plans, fees) are never linked this way.
	/// </summary>
	private static void LinkSimilarPayments(List<TxnInfo> pool, UnionFind groups)
	{
		var byAmount = pool.Select((t, i) => (t, i)).OrderBy(x => -x.t.T.Amount).ToList();
		for (var a = 0; a < byAmount.Count; a++)
		{
			var (ta, ia) = byAmount[a];
			var amountA = -ta.T.Amount;
			for (var b = a + 1; b < byAmount.Count; b++)
			{
				var (tb, ib) = byAmount[b];
				if (-tb.T.Amount > amountA * 1.03m + 0.01m)
				{
					break;
				}

				if (string.Equals(ta.SeriesKey, tb.SeriesKey, StringComparison.Ordinal) || IsContract(ta.Kind) || IsContract(tb.Kind))
				{
					continue;
				}

				if (Array.Exists(ta.Tokens, w => w.Length >= 5 && tb.Tokens.Contains(w, StringComparer.Ordinal)))
				{
					groups.Union(ia, ib);
				}
			}
		}
	}

	/// <summary>
	/// Determines whether the bank code says the payment is a contract: a mandate, loan, plan or fee.
	/// </summary>
	private static bool IsContract(Kind kind) => kind is Kind.DirectDebit or Kind.Loan or Kind.SavingsPlan or Kind.BankFee;

	/// <summary>
	/// Decides whether a group of payments is a fixed payment. Contracts need payments in at least two months, at
	/// most about one a month. Other payments need a regular rhythm (monthly ±7 days, quarterly or yearly ±12 days)
	/// and a stable amount (±10 % for cards, ±15 % otherwise). A group whose last payment is older than twice its
	/// rhythm has ended and is not suggested.
	/// </summary>
	private Suggestion? Evaluate(List<TxnInfo> payments, List<string> keys, string signature)
	{
		var kind = payments.GroupBy(x => x.Kind).OrderByDescending(g => g.Count()).First().Key;
		var amounts = payments.Select(x => -x.T.Amount).Order().ToList();
		var median = amounts[amounts.Count / 2];
		if (median < 1)
		{
			return null;
		}

		var intervals = payments.Zip(payments.Skip(1), (a, b) => b.T.Date.DayNumber - a.T.Date.DayNumber).Where(d => d > 0).ToList();
		var medianInterval = intervals.Count == 0 ? 0 : intervals.Order().ElementAt(intervals.Count / 2);
		int? cadence = medianInterval switch { >= 26 and <= 35 => 30, >= 84 and <= 98 => 91, >= 350 and <= 380 => 365, _ => null };

		string reason;
		bool high;
		if (IsContract(kind))
		{
			var months = payments.Select(x => x.Month).Distinct(StringComparer.Ordinal).Count();
			if (months < 2 || (double)payments.Count / months > 1.5)
			{
				return null;
			}

			cadence ??= 30;
			high = true;
			var contract = kind switch { Kind.DirectDebit => "direct debit", Kind.Loan => "loan instalment", Kind.SavingsPlan => "savings plan", _ => "bank fee" };
			reason = string.Create(CultureInfo.InvariantCulture, $"{contract} · {payments.Count} payments");
		}
		else
		{
			if (cadence is null || payments.Count < 2 || !IsRegular(intervals, cadence.Value) || !IsStable(amounts, median, kind))
			{
				return null;
			}

			high = payments.Count >= 3;
			var what = kind == Kind.Card ? "card payment" : "transfer";
			reason = string.Create(CultureInfo.InvariantCulture, $"{what} · {payments.Count}× {CadenceName(cadence.Value).ToLowerInvariant()}, same amount");
		}

		if (_today.DayNumber - payments[^1].T.Date.DayNumber > cadence.Value * 2 + 5)
		{
			return null;
		}

		var name = payments.GroupBy(x => x.Name, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Max(x => x.T.Date)).First().Key;
		var tokens = kind is Kind.Card or Kind.Refund
			? payments.Select(x => (IEnumerable<string>)x.Tokens).Aggregate((a, b) => a.Intersect(b, StringComparer.Ordinal)).ToList()
			: [];
		var category = payments.GroupBy(x => x.Category.Id.Value, StringComparer.Ordinal).OrderByDescending(g => g.Count()).First().Key;
		return new Suggestion(signature, name, category, median, cadence.Value, high, reason, payments, keys, tokens, kind);
	}

	/// <summary>
	/// Determines whether every interval is within the tolerance of the cadence: 7 days monthly, 12 otherwise.
	/// </summary>
	private static bool IsRegular(List<int> intervals, int cadence)
	{
		var tolerance = cadence == 30 ? 7 : 12;
		return intervals.TrueForAll(d => Math.Abs(d - cadence) <= tolerance);
	}

	/// <summary>
	/// Determines whether the amounts are stable: within 10 % (cards) or 15 % of the median, and with only two
	/// payments, equal to the cent (or 1 %).
	/// </summary>
	private static bool IsStable(List<decimal> amounts, decimal median, Kind kind)
	{
		var spread = kind == Kind.Card ? 0.10m : 0.15m;
		if (amounts.Exists(a => Math.Abs(a - median) > median * spread))
		{
			return false;
		}

		return amounts.Count != 2 || amounts[^1] - amounts[0] <= Math.Max(0.05m, median * 0.01m);
	}
	#endregion

	#region Monthly Totals
	/// <summary>
	/// Groups unconfirmed transactions by payee for the review queue.
	/// </summary>
	private List<ReviewItem> BuildReview() =>
		Items.Where(i => !i.CategoryConfirmed).GroupBy(i => i.SeriesKey, StringComparer.Ordinal)
			.Select(g => new ReviewItem(g.Key, g.First().Name, g.First().Category, g.Count(), g.Sum(i => i.T.Amount),
				g.Max(i => i.T.Date), g.First().T.Amount > 0, g.First().T.Description))
			.OrderByDescending(r => Math.Abs(r.Total)).ToList();

	/// <summary>
	/// Computes the totals of one month. Credits in an expense category (refunds) reduce spending. A split payment is
	/// spending as a whole and its parts feed the totals by category.
	/// </summary>
	private MonthSummary BuildMonth(string month)
	{
		var inMonth = Items.Where(i => string.Equals(i.Month, month, StringComparison.Ordinal)).ToList();
		var spend = inMonth.Where(i => i.EffectiveType == CategoryType.Expense).ToList();
		return new MonthSummary(month,
			inMonth.Where(i => i.IsIncome).Sum(i => i.T.Amount),
			spend.Where(i => i.Recurring is not null).Sum(i => -i.T.Amount),
			spend.Where(i => i.Recurring is null).Sum(i => -i.T.Amount),
			inMonth.Where(i => i.IsSaving).Sum(i => -i.T.Amount),
			spend.SelectMany(i => i.Parts).GroupBy(p => p.Category.Id.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(p => -p.Amount), StringComparer.Ordinal));
	}
	#endregion

	#region Text Helpers
	/// <summary>
	/// Normalises a name for comparison: upper case, letters only, words of two letters or more, at most four words.
	/// </summary>
	/// <param name="text">The name.</param>
	/// <returns>The normalised name, or the trimmed upper-case text when it has no such word.</returns>
	public static string Norm(string text)
	{
		var words = NonLetters().Replace(text.ToUpperInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).Take(4);
		var normalised = string.Join(' ', words);
		return normalised.Length > 0 ? normalised : text.Trim().ToUpperInvariant();
	}

	/// <summary>
	/// Extracts the distinctive words of a payee or shop name, used to recognise it when the bank label changes.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>The upper-case words of three letters or more that are not stop words, without duplicates.</returns>
	public static string[] Tokens(string name) =>
		NonLetters().Replace(name.ToUpperInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Where(w => w.Length >= 3 && !StopWords.Contains(w)).Distinct(StringComparer.Ordinal).ToArray();

	/// <summary>
	/// Extracts the shop name from a KBC card label: "WL*GOOGLE YouTube IED02 R296 Dublin Payment with…" →
	/// "GOOGLE YouTube". Stops at the country+postcode token (BE1000, IE2, IEUNKNOWN…), drops processor prefixes
	/// and numbers.
	/// </summary>
	/// <param name="text">The card label.</param>
	/// <returns>The shop name, at most 40 characters; title-cased when the label is all upper case.</returns>
	public static string Merchant(string text)
	{
		var head = PaymentWith().Split(text)[0].Replace('*', ' ').Replace('_', ' ');
		var words = head.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
		var cut = words.Count > 1 ? words.FindIndex(1, w => CountryPostcode().IsMatch(w)) : -1;
		if (cut > 0)
		{
			words = words.GetRange(0, cut);
		}

		while (words.Count > 1 && (ProcessorPrefixes.Contains(words[0].ToUpperInvariant()) || words[0].All(char.IsDigit)))
		{
			words.RemoveAt(0);
		}

		var name = string.Join(' ', words.Where(w => !w.All(char.IsDigit)));
		return Title(name.Length > 0 ? name : text);
	}

	/// <summary>
	/// Returns the text before the first occurrence of a marker (ignoring case), trimmed; the whole text when the
	/// marker is absent or at the start.
	/// </summary>
	private static string Before(string text, string marker)
	{
		var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		return (index > 0 ? text[..index] : text).Trim();
	}

	/// <summary>
	/// Cuts a name to 40 characters and title-cases it when it is all upper case (bank labels usually are).
	/// </summary>
	private static string Title(string text)
	{
		text = text.Trim();
		if (text.Length > 40)
		{
			text = text[..40].Trim();
		}

		return text.Any(char.IsLower) ? text : CultureInfo.GetCultureInfo("fr-BE").TextInfo.ToTitleCase(text.ToLowerInvariant());
	}

	/// <summary>
	/// Matches every character that is not an upper-case letter (accented ones included) or a space.
	/// </summary>
	[GeneratedRegex(@"[^A-ZÀ-ſ ]", RegexOptions.None, RegexTimeoutMilliseconds)]
	private static partial Regex NonLetters();

	/// <summary>
	/// Matches the "Payment with" separator of card labels, in English, French and Dutch.
	/// </summary>
	[GeneratedRegex(@"\s+(?:Payment with|Paiement avec|Betaling met|Payment via)\s", RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
	private static partial Regex PaymentWith();

	/// <summary>
	/// Matches a country code followed by a postcode or <c>UNKNOWN</c> (BE1000, IED02, IEUNKNOWN): the end of the
	/// shop name on card labels.
	/// </summary>
	[GeneratedRegex(@"^[A-Za-z]{2}(?:[A-Za-z]{0,2}\d+[A-Za-z0-9]*|UNKNOWN)$", RegexOptions.None, RegexTimeoutMilliseconds)]
	private static partial Regex CountryPostcode();

	/// <summary>
	/// Captures the mandate reference of a direct-debit description, in English, French and Dutch.
	/// </summary>
	[GeneratedRegex(@"(?:Direct debit|Domiciliation|Domiciliëring)\s+\d+\s+(?<mandate>\S+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, RegexTimeoutMilliseconds)]
	private static partial Regex Mandate();

	/// <summary>
	/// Captures the loan reference of a loan instalment description.
	/// </summary>
	[GeneratedRegex(@"Reference\s+(?<reference>\S+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, RegexTimeoutMilliseconds)]
	private static partial Regex LoanReference();
	#endregion

	#region Nested Types
	/// <summary>
	/// Author      : Gihed Annabi
	/// Date        : 11-2026
	/// Purpose     : Disjoint sets over indexes 0..n-1 with path halving, used to merge payments into suggestion
	///              groups (same series, or linked by amount and shop word).
	/// </summary>
	private sealed class UnionFind
	{
		#region Fields
		private readonly int[] _parent;
		#endregion

		#region Constructors
		/// <summary>
		/// Initialises a new <see cref="UnionFind"/> where every index is its own group.
		/// </summary>
		/// <param name="count">The number of indexes.</param>
		public UnionFind(int count) => _parent = Enumerable.Range(0, count).ToArray();
		#endregion

		#region Public Methods
		/// <summary>
		/// Finds the representative of an index's group.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <returns>The representative index.</returns>
		public int Find(int index)
		{
			while (_parent[index] != index)
			{
				_parent[index] = _parent[_parent[index]];
				index = _parent[index];
			}

			return index;
		}

		/// <summary>
		/// Merges the groups of two indexes; the second group's representative becomes the representative.
		/// </summary>
		/// <param name="a">An index of the first group.</param>
		/// <param name="b">An index of the second group.</param>
		public void Union(int a, int b) => _parent[Find(a)] = Find(b);
		#endregion
	}
	#endregion
}
