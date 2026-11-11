using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Application.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Errors shared by the backend's use cases, coded <c>Backend.Property.Reason</c>.
/// </summary>
public static class BackendErrors
{
	#region Static Instances
	/// <summary>
	/// The database has not been initialised yet: neither the JSON archive was imported nor a fresh start chosen.
	/// Writes are refused until then, because any write would make the one-time import impossible.
	/// </summary>
	public static readonly Error NotInitialized = new("Backend.Database.NotInitialized",
		"The backend has no data yet. Import the JSON archive (--import-json) or start fresh (--initialize) first.");
	#endregion
}
