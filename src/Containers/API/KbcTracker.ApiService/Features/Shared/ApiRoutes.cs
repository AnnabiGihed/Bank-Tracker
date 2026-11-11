namespace KbcTracker.ApiService.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Names every backend route in one place, relative to the versioned prefix.
/// </summary>
public static class ApiRoutes
{
	#region Constants
	/// <summary>The versioned resource prefix.</summary>
	public const string Prefix = "api/v1";
	/// <summary>The public process-health route.</summary>
	public const string Health = "/health";
	/// <summary>The absolute MVC process-health route.</summary>
	public const string HealthTemplate = "~/health";
	/// <summary>The paged history route.</summary>
	public const string Transactions = "transactions";
	/// <summary>The category-definitions route.</summary>
	public const string Categories = "categories";
	/// <summary>The safe bank-status route.</summary>
	public const string Connection = "connection";
	/// <summary>The manual sync route.</summary>
	public const string Sync = "sync";
	/// <summary>The supported-banks route.</summary>
	public const string Banks = "banks";
	/// <summary>The route that starts a bank consent.</summary>
	public const string Consent = "consent";
	/// <summary>The route the bank returns the user to after consent.</summary>
	public const string ConsentCallback = "consent/callback";
	/// <summary>The classified-workspace route.</summary>
	public const string Workspace = "workspace";
	/// <summary>The user-decision route.</summary>
	public const string Decisions = "decisions";
	#endregion
}
