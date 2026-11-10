using System.Globalization;

namespace KbcTracker.Web.Features.BankConnection;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The line <c>--sync</c> prints for the daily scheduled task: the error, or how many transactions were
///              added and, when some could not be read, how many are still left out.
/// </summary>
internal static class SyncReport
{
	#region Public Methods
	/// <summary>
	/// Describes the outcome of a sync in one line.
	/// </summary>
	/// <param name="added">How many new transactions were stored.</param>
	/// <param name="error">The error of a failed sync, or <see langword="null"/> when it succeeded.</param>
	/// <param name="skipped">How many transactions the bank sent that could not be read and are still left out.</param>
	/// <returns>The error, or <c>OK, N new transactions</c> followed by the left-out count when it is not 0.</returns>
	public static string Describe(int added, string? error, int skipped)
	{
		if (error is not null)
		{
			return error;
		}

		var leftOut = skipped > 0 ? string.Create(CultureInfo.InvariantCulture, $", {skipped} could not be read and were left out") : "";
		return string.Create(CultureInfo.InvariantCulture, $"OK, {added} new transactions{leftOut}");
	}
	#endregion
}
