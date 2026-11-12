using System.Text.Json;
using KbcTracker.Application.Features.BankConnection.Services;
using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Infrastructure.Features.Legacy;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Categories;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared.Import;
using Microsoft.EntityFrameworkCore;

namespace KbcTracker.ApiService.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Runs explicit local maintenance commands without starting HTTP listeners or modifying the JSON archive.
///
///              A new database must be filled once before the API accepts writes: <c>--import-json</c> brings the
///              existing history and refuses a database that already holds data; <c>--initialize</c> seeds the default
///              categories (<see cref="CategoryDefaults.CreateDefaults"/>, with the keyword pack of the configured
///              <c>EnableBanking:Country</c>, <c>BE</c> by default) and empty decisions, and leaves a database that already
///              holds data alone, except that it upgrades a flat category list to the category tree once
///              (<see cref="CategoryTreeUpgrade"/>), so the Aspire AppHost can run it on every start.
/// </summary>
public static class BackendAdministration
{
	#region Public Methods
	/// <summary>Identifies maintenance invocations that must exit rather than start the server.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>Whether an administrative switch is present.</returns>
	public static bool IsRequested(string[] args) => args.Any(a => a is "--migrate" or "--import-json" or "--initialize" or "--sync");

	/// <summary>Runs one migration, empty-database import or initialisation, or manual sync and returns its process
	/// exit code.</summary>
	/// <param name="args">Exactly one command; import also requires the archive directory.</param>
	/// <param name="services">The configured backend services.</param>
	/// <param name="cancellationToken">Cancels the operation.</param>
	/// <returns>Zero on success, one on operation failure, or two on invalid arguments.</returns>
	public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(args);
		ArgumentNullException.ThrowIfNull(services);
		if (!(args is ["--migrate"] or ["--sync"] or ["--initialize"] or ["--import-json", _])
			|| (args.Length == 2 && (string.IsNullOrWhiteSpace(args[1]) || args[1].StartsWith("--", StringComparison.Ordinal))))
		{
			await Console.Error.WriteLineAsync("Use --migrate, --import-json <archive-directory>, --initialize, or --sync, one command at a time.").ConfigureAwait(false);
			return 2;
		}

		try
		{
			var scope = services.CreateAsyncScope();
			await using (scope.ConfigureAwait(false))
			{
				return await ExecuteCommandAsync(args, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
		{
			await Console.Error.WriteLineAsync("The archive could not be read. Check its required files, JSON format and identifiers; the source archive was not changed.").ConfigureAwait(false);
			return 1;
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Executes the validated command within one dependency scope.</summary>
	private static async Task<int> ExecuteCommandAsync(string[] args, IServiceProvider services, CancellationToken cancellationToken)
	{
		if (args[0] is "--migrate")
		{
			await services.GetRequiredService<KbcTrackerDbContext>().Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
			return 0;
		}

		if (args[0] is "--sync")
		{
			var (added, error) = await services.GetRequiredService<SyncService>().SyncAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			Console.WriteLine(error is null ? $"Sync completed: {added} new transactions." : "Sync failed. Check the backend configuration and connection status.");
			return error is null ? 0 : 1;
		}

		if (args[0] is "--initialize")
		{
			return await InitializeAsync(services, cancellationToken).ConfigureAwait(false);
		}

		var snapshot = await LegacyJsonReader.ReadAsync(args[1], cancellationToken).ConfigureAwait(false);
		var result = await services.GetRequiredService<DataImporter>().ImportAsync(
			new DataSnapshot(snapshot.Transactions, snapshot.Categories, snapshot.UserData, snapshot.Session), cancellationToken).ConfigureAwait(false);
		if (result.IsFailure)
		{
			await Console.Error.WriteLineAsync(result.Error == ImportErrors.DatabaseNotEmpty
				? "Import refused: the database already contains data. Keep the archive and verify the intended target."
				: "Import failed; the snapshot was not committed. Check the database configuration.").ConfigureAwait(false);
			return 1;
		}

		Console.WriteLine($"Imported {snapshot.Transactions.Count} transactions and {snapshot.Categories.Count} categories. The source archive was not changed.");
		return 0;
	}

	/// <summary>Seeds an empty database with the default category tree and empty decisions, through the same
	/// all-or-nothing import as <c>--import-json</c>. A database that already holds data keeps its categories, except
	/// that a flat list from before the tree is upgraded once to the tree, so running it on every start never
	/// changes what the owner has decided.</summary>
	private static async Task<int> InitializeAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		var country = services.GetRequiredService<IConfiguration>()["EnableBanking:Country"];
		var categories = CategoryDefaults.CreateDefaults(string.IsNullOrWhiteSpace(country) ? CategoryDefaults.DefaultCountry : country).ToList();
		var result = await services.GetRequiredService<DataImporter>().ImportAsync(
			new DataSnapshot([], categories, UserData.Empty(), null), cancellationToken).ConfigureAwait(false);
		if (result.IsFailure && result.Error != ImportErrors.DatabaseNotEmpty)
		{
			await Console.Error.WriteLineAsync("Initialisation failed; nothing was committed. Check the database configuration.").ConfigureAwait(false);
			return 1;
		}

		if (result.IsSuccess)
		{
			Console.WriteLine($"Seeded {categories.Count} categories and no history.");
			return 0;
		}

		Console.WriteLine("The database already holds data; nothing was seeded.");
		var upgrade = await services.GetRequiredService<CategoryTreeUpgrade>().UpgradeAsync(
			string.IsNullOrWhiteSpace(country) ? CategoryDefaults.DefaultCountry : country, cancellationToken).ConfigureAwait(false);
		if (upgrade.IsFailure)
		{
			await Console.Error.WriteLineAsync("The category tree upgrade failed; nothing was committed. Check the database configuration.").ConfigureAwait(false);
			return 1;
		}

		if (upgrade.Value)
		{
			Console.WriteLine("The flat category list was upgraded to the category tree.");
		}

		return 0;
	}
	#endregion
}
