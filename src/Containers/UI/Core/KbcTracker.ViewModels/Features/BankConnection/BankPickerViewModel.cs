namespace KbcTracker.ViewModels.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What the bank picker shows: the banks Enable Banking offers, KBC banks first and the rest by name, and
///              the call that starts the consent for the bank the owner chose.
/// </summary>
public sealed class BankPickerViewModel
{
	#region Fields
	/// <summary>The typed client of the backend.</summary>
	private readonly BackendClient _backend;
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="BankPickerViewModel"/>.</summary>
	/// <param name="backend">The typed client of the backend.</param>
	/// <param name="banks">The banks the backend lists.</param>
	/// <param name="formats">The formats whose culture decides the order by name.</param>
	/// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
	public BankPickerViewModel(BackendClient backend, IEnumerable<BankDefinition> banks, Formats formats)
	{
		ArgumentNullException.ThrowIfNull(backend);
		ArgumentNullException.ThrowIfNull(banks);
		ArgumentNullException.ThrowIfNull(formats);
		_backend = backend;
		Banks = [.. banks.OrderByDescending(b => b.Name.Contains("KBC", StringComparison.OrdinalIgnoreCase)).ThenBy(b => b.Name, formats.NameComparer)];
	}
	#endregion

	#region Properties
	/// <summary>Gets the banks, KBC banks first.</summary>
	public IReadOnlyList<BankDefinition> Banks { get; }
	#endregion

	#region Public Methods
	/// <summary>Reads the banks from the backend.</summary>
	/// <param name="backend">The typed client of the backend.</param>
	/// <param name="formats">The formats whose culture decides the order by name.</param>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The view model, or the problem to show, such as bank access not being set up on the backend.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="backend"/> or <paramref name="formats"/> is <see langword="null"/>.</exception>
	public static async Task<BackendResult<BankPickerViewModel>> LoadAsync(BackendClient backend, Formats formats, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(backend);
		ArgumentNullException.ThrowIfNull(formats);
		var result = await BackendCall.RunAsync(backend.GetBanksAsync, cancellationToken).ConfigureAwait(false);
		return result.IsSuccess
			? new BackendResult<BankPickerViewModel>(new BankPickerViewModel(backend, result.Value ?? [], formats), null)
			: new BackendResult<BankPickerViewModel>(default, result.Problem!);
	}

	/// <summary>Starts the consent of a bank on the backend.</summary>
	/// <param name="bank">The bank the owner chose.</param>
	/// <param name="cancellationToken">Cancels the call.</param>
	/// <returns>The address of the bank's consent page, or the problem to show.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="bank"/> is <see langword="null"/>.</exception>
	public async Task<BackendResult<Uri>> BeginConsentAsync(BankDefinition bank, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(bank);
		var result = await BackendCall.RunReplyAsync(ct => _backend.BeginConsentAsync(new BeginConsentRequest(bank.Name, bank.Country), ct), cancellationToken)
			.ConfigureAwait(false);
		if (!result.IsSuccess)
		{
			return new BackendResult<Uri>(default, result.Problem!);
		}

		return result.Value is null
			? new BackendResult<Uri>(default, new BackendProblem(502, "Request could not complete.", "The backend returned no bank address."))
			: new BackendResult<Uri>(result.Value.AuthorizationUrl, null);
	}
	#endregion
}
