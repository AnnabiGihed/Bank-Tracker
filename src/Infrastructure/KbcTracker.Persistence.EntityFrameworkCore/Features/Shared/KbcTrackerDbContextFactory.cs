using Microsoft.EntityFrameworkCore.Design;

namespace KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Creates the context for the EF Core tools (<c>dotnet ef migrations add</c>), which need no
///              database. The connection string has no password on purpose: the tools never connect, and the real
///              one comes from configuration at run time.
/// </summary>
internal sealed class KbcTrackerDbContextFactory : IDesignTimeDbContextFactory<KbcTrackerDbContext>
{
	#region Constants
	/// <summary>
	/// A local connection string used only to build the model for the tools.
	/// </summary>
	private const string DesignTimeConnectionString = "Host=localhost;Database=kbctracker;Username=kbctracker";
	#endregion

	#region IDesignTimeDbContextFactory Implementation
	/// <inheritdoc />
	public KbcTrackerDbContext CreateDbContext(string[] args) =>
		new(new DbContextOptionsBuilder<KbcTrackerDbContext>().UseNpgsql(DesignTimeConnectionString).Options);
	#endregion
}
