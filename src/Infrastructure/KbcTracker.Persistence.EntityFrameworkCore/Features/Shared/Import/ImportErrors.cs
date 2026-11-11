namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The errors the one-time data import can fail with, coded <c>Import.Property.Reason</c>.
/// </summary>
public static class ImportErrors
{
	#region Static Instances
	/// <summary>
	/// The database already holds data, so importing again could duplicate or overwrite it.
	/// </summary>
	public static readonly Error DatabaseNotEmpty = new("Import.Database.NotEmpty", "The database already holds data; the import runs only once, into an empty database.");
	#endregion
}
