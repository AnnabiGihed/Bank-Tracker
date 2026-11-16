namespace KbcTracker.Domain.Features.BankConnection.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Knows the owner's own accounts by IBAN, so the analysis can tell a payment to one of them from a payment to
///              someone else, and a transfer into a savings account from one between current accounts (ADR-0010).
///
///              A target account is savings only when the bank calls it so (<c>SVGS</c>) and the money does not leave a savings
///              account. Without an account type nothing is guessed: such a transfer stays an ordinary own transfer.
/// </summary>
internal sealed class OwnAccountDirectory
{
	#region Static Instances
	/// <summary>
	/// A directory with no account: only the holder-name rule recognises own transfers.
	/// </summary>
	public static readonly OwnAccountDirectory None = new([]);
	#endregion

	#region Fields
	/// <summary>
	/// The accounts by compact upper-case IBAN; accounts without an IBAN are not listed.
	/// </summary>
	private readonly Dictionary<string, OwnAccount> _byIban;

	/// <summary>
	/// The accounts by Enable Banking id.
	/// </summary>
	private readonly Dictionary<string, OwnAccount> _byUid;
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="OwnAccountDirectory"/>.
	/// </summary>
	/// <param name="accounts">The accounts the bank connection covers.</param>
	public OwnAccountDirectory(IEnumerable<OwnAccount> accounts)
	{
		var list = accounts.ToList();
		_byIban = list.Where(a => OwnAccount.NormaliseIban(a.Iban) is not null)
			.GroupBy(a => OwnAccount.NormaliseIban(a.Iban)!, StringComparer.Ordinal)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
		_byUid = list.GroupBy(a => a.Uid, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
	}
	#endregion

	#region Public Methods
	/// <summary>
	/// Finds the own account with an IBAN.
	/// </summary>
	/// <param name="iban">The IBAN as the bank wrote it, or <see langword="null"/>.</param>
	/// <returns>The account, or <see langword="null"/> when the IBAN is blank or belongs to someone else.</returns>
	public OwnAccount? Find(string? iban)
	{
		var compact = OwnAccount.NormaliseIban(iban);
		return compact is not null && _byIban.TryGetValue(compact, out var account) ? account : null;
	}

	/// <summary>
	/// Tells whether money moved out of one own account into another is saved money.
	/// </summary>
	/// <param name="sourceUid">The Enable Banking id of the account the money leaves.</param>
	/// <param name="target">The account the money enters.</param>
	/// <returns><see langword="true"/> when the target is a savings account for this move.</returns>
	public bool IsSaving(string sourceUid, OwnAccount target)
	{
		ArgumentNullException.ThrowIfNull(target);
		if (!string.Equals(target.AccountType, OwnAccount.SavingsType, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var leavesSavings = _byUid.TryGetValue(sourceUid, out var source)
			&& string.Equals(source.AccountType, OwnAccount.SavingsType, StringComparison.OrdinalIgnoreCase);
		return !leavesSavings;
	}
	#endregion
}
