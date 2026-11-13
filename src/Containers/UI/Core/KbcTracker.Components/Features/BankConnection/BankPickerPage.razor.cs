namespace KbcTracker.Components.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The bank picker (<c>/banks</c>): the banks the aggregator offers, KBC first. Connect asks the backend to
///              start the consent and hands the bank's address to the host's <see cref="IBankConnectionLauncher"/> (the web
///              app leaves for the bank's login page; the Android app cannot, and says so). It does not need the workspace,
///              so it works before any data exists.
/// </summary>
public sealed partial class BankPickerPage : ComponentBase, IDisposable
{
	#region Injected Services
	/// <summary>Gets or sets the typed client of the backend.</summary>
	[Inject]
	public required BackendClient Backend { get; set; }

	/// <summary>Gets or sets the host's way to open the bank's consent page.</summary>
	[Inject]
	public required IBankConnectionLauncher Launcher { get; set; }

	/// <summary>Gets or sets the service that shows notifications.</summary>
	[Inject]
	public required NotificationService Notifications { get; set; }
	#endregion

	#region Fields
	/// <summary>Cancelled when the page goes away.</summary>
	private readonly CancellationTokenSource _lifetime = new();

	/// <summary>The banks to choose from, or <see langword="null"/> until they are loaded.</summary>
	private BankPickerViewModel? _picker;

	/// <summary>Why the banks could not be loaded, or <see langword="null"/>.</summary>
	private BackendProblem? _problem;

	/// <summary>The bank whose consent is being started, or <see langword="null"/>.</summary>
	private BankDefinition? _connecting;
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override async Task OnInitializedAsync() => await LoadAsync();
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		_lifetime.Cancel();
		_lifetime.Dispose();
		GC.SuppressFinalize(this);
	}
	#endregion

	#region Event Handlers
	/// <summary>Starts the consent with a bank and opens its login page through the host.</summary>
	/// <param name="bank">The bank the owner chose.</param>
	/// <returns>A task that completes when the host was asked to open the bank, or the refusal was shown.</returns>
	private async Task ConnectAsync(BankDefinition bank)
	{
		_connecting = bank;
		try
		{
			var result = await _picker!.BeginConsentAsync(bank, _lifetime.Token);
			if (result.Problem is { } problem)
			{
				ProblemNotices.Report(Notifications, problem);
				return;
			}

			await Launcher.LaunchAsync(result.Value!, _lifetime.Token);
		}
		catch (OperationCanceledException)
		{
			// The page went away; the bank's page is not opened.
		}
		finally
		{
			_connecting = null;
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Loads the banks, or the reason they could not be loaded.</summary>
	/// <returns>A task that completes when the banks were requested.</returns>
	private async Task LoadAsync()
	{
		try
		{
			var result = await BankPickerViewModel.LoadAsync(Backend, Formats.Belgian, _lifetime.Token);
			_picker = result.Value;
			_problem = result.Problem;
		}
		catch (OperationCanceledException)
		{
			// The page went away while the banks were loading.
		}
	}
	#endregion
}
