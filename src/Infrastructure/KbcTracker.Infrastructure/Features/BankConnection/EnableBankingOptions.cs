namespace KbcTracker.Infrastructure.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Settings of the Enable Banking connection, bound from the <c>EnableBanking</c> configuration
///              section. The private key itself is never stored here, only the path to it.
/// </summary>
public sealed class EnableBankingOptions
{
	#region Properties
	/// <summary>
	/// Gets or sets the base address of the Enable Banking API.
	/// </summary>
	public Uri BaseUrl { get; set; } = new("https://api.enablebanking.com");

	/// <summary>
	/// Gets or sets the application id from the Enable Banking Control Panel; it is the <c>kid</c> of every JWT.
	/// </summary>
	public string ApplicationId { get; set; } = "";

	/// <summary>
	/// Gets or sets the path of the application's private key (<c>.pem</c>). Keep the file outside the repository.
	/// </summary>
	public string PrivateKeyPath { get; set; } = "";

	/// <summary>
	/// Gets or sets where the bank sends the user back after consent; it must be registered in the Control Panel.
	/// </summary>
	public string RedirectUrl { get; set; } = "https://localhost:5001/callback";

	/// <summary>
	/// Gets or sets the ISO country code of the banks offered.
	/// </summary>
	public string Country { get; set; } = "BE";

	/// <summary>
	/// Gets or sets how many days the consent asked from the bank lasts (180 at most).
	/// </summary>
	public int ConsentDays { get; set; } = 180;

	/// <summary>
	/// Gets or sets how many months back a full sync asks for; KBC returns about 90 days whatever is asked.
	/// </summary>
	public int HistoryMonths { get; set; } = 13;
	#endregion
}
