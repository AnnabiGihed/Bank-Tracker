using System.Text.Json;
using KbcTracker.Infrastructure.Features.Legacy;

namespace KbcTracker.Infrastructure.Tests.Features.Legacy;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies archive import reads preserve legacy data without writing files or hiding corruption.
/// </summary>
public sealed class LegacyJsonReaderTests : IDisposable
{
	#region Constants
	/// <summary>A stored historical transaction independent of the bank's current retrieval window.</summary>
	private const string Transaction = """
		{"Id":"historical","AccountUid":"account","Date":"2020-01-02","Amount":-123.4500,"Currency":"EUR","Counterparty":"Shop","Description":"Old payment","Raw":"{ \"z\": 1, \"a\": 2 }"}
		""";
	#endregion

	#region Fields
	/// <summary>The test's own disposable archive; no personal data folders are read.</summary>
	private readonly string _directory = Path.Combine(Path.GetTempPath(), "KbcTracker-import-" + Guid.NewGuid().ToString("N"));
	#endregion

	#region Archive Tests
	/// <summary>Verifies amounts, raw JSON, decisions and consent survive the archive read unchanged.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_CompleteArchive_PreservesHistoryAndDecisionsWithoutWriting()
	{
		await WriteArchiveAsync();
		await WriteAsync("session.json", """
			{"SessionId":"private-consent","AccountUids":["account"],"ConnectedAt":"2026-11-01T09:00:00+01:00","ValidUntil":"2027-05-01T09:00:00+02:00","LastSync":"2026-11-02T12:00:00Z","LastError":"previous failure","SkippedTransactions":4}
			""");
		var before = Directory.GetFiles(_directory).ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal);

		var snapshot = await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None);

		var transaction = snapshot.Transactions.Should().ContainSingle().Subject;
		transaction.Id.Value.Should().Be("historical");
		transaction.Date.Should().Be(new DateOnly(2020, 1, 2));
		decimal.GetBits(transaction.Amount).Should().Equal(decimal.GetBits(-123.4500m));
		transaction.Raw.Should().Be("{ \"z\": 1, \"a\": 2 }");
		snapshot.Categories.Should().ContainSingle().Which.Name.Should().Be("Other");
		snapshot.UserData.DismissedSuggestions.Should().Contain("dismissed");
		snapshot.UserData.PayeeNames.Should().ContainKey("series").WhoseValue.Should().Be("Renamed");
		snapshot.UserData.Recurring.Should().ContainSingle().Which.Id.Value.Should().Be("fixed");
		snapshot.Session.Should().NotBeNull();
		snapshot.Session.Id.Value.Should().Be("private-consent");
		snapshot.Session.SkippedTransactions.Should().Be(4);
		Directory.GetFiles(_directory).ToDictionary(path => path, File.ReadAllText, StringComparer.Ordinal).Should().BeEquivalentTo(before);
	}

	/// <summary>Verifies a disconnected archive imports without manufacturing a bank consent file.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_NoSession_ReturnsDisconnectedSnapshot()
	{
		await WriteArchiveAsync();

		var snapshot = await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None);

		snapshot.Session.Should().BeNull();
		File.Exists(Path.Combine(_directory, "session.json")).Should().BeFalse();
	}

	/// <summary>Verifies a required missing file is refused instead of erasing a portion of the archive.</summary>
	/// <param name="name">The required file removed from this temporary archive.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("transactions.json")]
	[InlineData("categories.json")]
	public async Task ReadAsync_RequiredFileMissing_Throws(string name)
	{
		await WriteArchiveAsync();
		File.Delete(Path.Combine(_directory, name));

		var read = async () => await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None).ConfigureAwait(false);

		await read.Should().ThrowAsync<FileNotFoundException>();
	}

	/// <summary>Verifies an archive without decisions reads as no decisions, as the web app wrote that file only on the
	/// user's first decision.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_NoUserData_EmptyDecisions()
	{
		await WriteArchiveAsync();
		File.Delete(Path.Combine(_directory, "userdata.json"));

		var snapshot = await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None);

		snapshot.UserData.Recurring.Should().BeEmpty();
		snapshot.UserData.Payees.Should().BeEmpty();
		snapshot.Transactions.Should().NotBeEmpty();
	}

	/// <summary>Verifies corruption and ambiguous rows never become an empty successful import.</summary>
	/// <param name="name">The file to corrupt.</param>
	/// <param name="content">The invalid archive content.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("transactions.json", "null")]
	[InlineData("transactions.json", "[null]")]
	[InlineData("categories.json", "[{\"Id\":\"\"}]")]
	[InlineData("userdata.json", "null")]
	[InlineData("userdata.json", "{\"Recurring\":[null]}")]
	public async Task ReadAsync_InvalidArchive_ThrowsWithoutWriting(string name, string content)
	{
		await WriteArchiveAsync();
		await WriteAsync(name, content);
		var read = async () => await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None).ConfigureAwait(false);

		await read.Should().ThrowAsync<InvalidDataException>();
		(await File.ReadAllTextAsync(Path.Combine(_directory, name))).Should().Be(content);
	}

	/// <summary>Verifies syntactically broken JSON fails and leaves the source intact.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_MalformedJson_Throws()
	{
		await WriteArchiveAsync();
		await WriteAsync("transactions.json", "[");
		var read = async () => await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None).ConfigureAwait(false);

		await read.Should().ThrowAsync<JsonException>();
	}

	/// <summary>Verifies duplicate stored identifiers cannot silently overwrite one another.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_DuplicateTransactionIdentifiers_Throws()
	{
		await WriteArchiveAsync();
		await WriteAsync("transactions.json", $"[{Transaction},{Transaction}]");
		var read = async () => await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None).ConfigureAwait(false);

		await read.Should().ThrowAsync<InvalidDataException>();
	}

	/// <summary>Verifies legacy blank transaction ids use the same stable identity as subsequent loads.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task ReadAsync_LegacyBlankTransactionIdentifier_DerivedIdenticallyEveryRead()
	{
		await WriteArchiveAsync();
		await WriteAsync("transactions.json", "[{\"Id\":\"\",\"AccountUid\":\"account\",\"Date\":\"2020-01-02\",\"Amount\":-1,\"Raw\":\"{\\\"entry_reference\\\":\\\"bank-reference\\\"}\"}]");

		var first = await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None);
		var second = await LegacyJsonReader.ReadAsync(_directory, CancellationToken.None);

		first.Transactions.Single().Id.Should().Be(second.Transactions.Single().Id);
		first.Transactions.Single().Id.Value.Should().Be("bank-reference");
	}
	#endregion

	#region IDisposable Implementation
	/// <inheritdoc />
	public void Dispose()
	{
		if (Directory.Exists(_directory))
		{
			Directory.Delete(_directory, recursive: true);
		}
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds the minimum complete archive with representative decisions.</summary>
	private async Task WriteArchiveAsync()
	{
		Directory.CreateDirectory(_directory);
		await WriteAsync("transactions.json", $"[{Transaction}]").ConfigureAwait(false);
		await WriteAsync("categories.json", "[{\"Id\":\"other\",\"Name\":\"Other\",\"Type\":\"Expense\",\"BuiltIn\":true}]").ConfigureAwait(false);
		await WriteAsync("userdata.json", """
			{"Recurring":[{"Id":"fixed","Name":"Rent","SeriesKeys":["series"],"Amount":940}],"DismissedSuggestions":["dismissed"],"PayeeNames":{"series":"Renamed"}}
			""").ConfigureAwait(false);
	}

	/// <summary>Writes only within this test's temporary archive.</summary>
	private Task WriteAsync(string name, string content) => File.WriteAllTextAsync(Path.Combine(_directory, name), content);
	#endregion
}
