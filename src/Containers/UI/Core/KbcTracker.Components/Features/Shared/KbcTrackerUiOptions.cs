namespace KbcTracker.Components.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : What a host tells the shared components about itself. A host that can serve the CSV export (the web
///              app) sets <see cref="ExportCsvPath"/>; a host that cannot (the Android app) leaves it empty and Settings
///              shows no export link.
/// </summary>
public sealed class KbcTrackerUiOptions
{
	#region Properties
	/// <summary>Gets or sets the address the browser can download the CSV export from, or <see langword="null"/> when the host offers none.</summary>
	public string? ExportCsvPath { get; set; }
	#endregion
}
