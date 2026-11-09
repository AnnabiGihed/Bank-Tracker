namespace KbcTracker.Domain.Tests.Features.Shared.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : A <see cref="FactAttribute"/> for tests that read the user's real data folder: the test is skipped,
///              not passed, on a machine where that data does not exist.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RealDataFactAttribute : FactAttribute
{
	#region Static Instances
	/// <summary>
	/// The web host's data folder when <c>DataDirectory</c> is not configured.
	/// </summary>
	public static readonly string DataDir =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KbcTracker");

	/// <summary>
	/// The real transaction history file.
	/// </summary>
	public static readonly string TransactionsFile = Path.Combine(DataDir, "transactions.json");

	/// <summary>
	/// The real category definitions file.
	/// </summary>
	public static readonly string CategoriesFile = Path.Combine(DataDir, "categories.json");
	#endregion

	#region Constructors
	/// <summary>
	/// Initialises a new <see cref="RealDataFactAttribute"/>, which skips the test when the real data is missing.
	/// </summary>
	public RealDataFactAttribute()
	{
		if (!File.Exists(TransactionsFile) || !File.Exists(CategoriesFile))
		{
			Skip = $"No real data in {DataDir}.";
		}
	}
	#endregion
}
