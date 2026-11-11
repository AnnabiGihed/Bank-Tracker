using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using KbcTracker.ApiService.Features.Shared.Security;
using KbcTracker.Application.Features.Transactions.Queries;

namespace KbcTracker.ApiService.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies HTTP contracts, privacy and access controls through the real PostgreSQL backend.
/// </summary>
/// <param name="fixture">The shared isolated backend.</param>
public sealed class BackendTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
	#region Read Tests
	/// <summary>Verifies history comes from PostgreSQL through the mediator and omits private bank fields.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Transactions_Get_PagedSafeHistory()
	{
		using var client = fixture.CreateClient();
		using var response = await client.GetAsync(new Uri("/api/v1/transactions?take=1", UriKind.Relative));
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		var rows = await response.Content.ReadFromJsonAsync<List<TransactionSummary>>();
		rows.Should().NotBeNull();
		rows.Should().ContainSingle();
		var body = await response.Content.ReadAsStringAsync();
		body.Should().NotContain(ApiFixture.PrivateSession).And.NotContain(ApiFixture.RawMarker).And.NotContain("BE00111122223333");
	}

	/// <summary>Verifies invalid pages receive a safe validation problem instead of an unbounded query.</summary>
	/// <param name="query">The invalid paging values.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[InlineData("skip=-1")]
	[InlineData("take=0")]
	[InlineData("take=501")]
	public async Task Transactions_InvalidPage_ValidationProblem(string query)
	{
		using var client = fixture.CreateClient();
		using var response = await client.GetAsync(new Uri("/api/v1/transactions?" + query, UriKind.Relative));
		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
		body.RootElement.GetProperty("type").GetString().Should().Be("Transactions.Page.Invalid");
	}

	/// <summary>Verifies the status response discloses neither session credentials nor provider errors.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Connection_Get_StatusWithoutCredentials()
	{
		using var client = fixture.CreateClient();
		var text = await client.GetStringAsync(new Uri("/api/v1/connection", UriKind.Relative));
		using var status = JsonDocument.Parse(text);
		status.RootElement.GetProperty("connected").GetBoolean().Should().BeTrue();
		text.Should().NotContain(ApiFixture.PrivateSession).And.NotContain("private file paths").And.NotContain("accountUids");
	}

	/// <summary>Verifies stored category definitions can be read independently of a JSON folder.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Categories_Get_StoredDefinitions()
	{
		using var client = fixture.CreateClient();
		var text = await client.GetStringAsync(new Uri("/api/v1/categories", UriKind.Relative));
		using var categories = JsonDocument.Parse(text);
		categories.RootElement.GetArrayLength().Should().Be(1);
		categories.RootElement[0].GetProperty("name").GetString().Should().Be("Other");
	}
	#endregion

	#region Sync Tests
	/// <summary>Verifies repeated HTTP syncs persist a batch once and retain older history.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Sync_PostTwice_NoDuplicateHistory()
	{
		using var client = fixture.CreateClient();
		using var first = await client.PostAsync(new Uri("/api/v1/sync", UriKind.Relative), content: null);
		first.StatusCode.Should().Be(HttpStatusCode.OK);
		using var second = await client.PostAsync(new Uri("/api/v1/sync", UriKind.Relative), content: null);
		second.StatusCode.Should().Be(HttpStatusCode.OK);
		using var result = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
		result.RootElement.GetProperty("added").GetInt32().Should().Be(0);
		var rows = await client.GetFromJsonAsync<List<TransactionSummary>>(new Uri("/api/v1/transactions", UriKind.Relative));
		rows.Should().NotBeNull();
		rows.Select(t => t.Id).Should().BeEquivalentTo("historical", "downloaded");
	}

	/// <summary>Verifies a requested full sync asks the bank for the longest history even when history exists.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Sync_FullRequested_AsksLongestHistory()
	{
		var bank = ApiFixture.DownloadingSource();
		using var factory = fixture.CreateFactory("Development", transactionSource: bank);
		using var client = factory.CreateClient();
		using var response = await client.PostAsync(new Uri("/api/v1/sync?full=true", UriKind.Relative), content: null);
		response.StatusCode.Should().Be(HttpStatusCode.OK);
		await bank.Received(1).GetTransactionsAsync("current", Arg.Any<DateOnly?>(), true);
		await bank.DidNotReceive().GetTransactionsAsync(Arg.Any<string>(), Arg.Any<DateOnly?>(), false);
	}
	#endregion

	#region Access And Contract Tests
	/// <summary>Verifies production rejects requests without identity and accepts the configured owner behind loopback.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Production_OwnerIdentityRequired_AndSwaggerNotPublished()
	{
		using var factory = fixture.CreateFactory("Production");
		using var client = factory.CreateClient();
		using var denied = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));
		denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
		client.DefaultRequestHeaders.Add(TailnetAccessMiddleware.IdentityHeader, ApiFixture.OwnerLogin);
		using var allowed = await client.GetAsync(new Uri("/api/v1/connection", UriKind.Relative));
		allowed.StatusCode.Should().Be(HttpStatusCode.OK);
		using var swagger = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
		swagger.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	/// <summary>Verifies development exposes documented operations and matches the committed OpenAPI contract.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Development_OpenApi_MatchesCommittedContract()
	{
		using var client = fixture.CreateClient();
		var generated = await client.GetStringAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
		var committed = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Contracts", "openapi.json"));
		JsonNode.DeepEquals(JsonNode.Parse(generated), JsonNode.Parse(committed)).Should().BeTrue("API changes must update the committed specification");
		using var document = JsonDocument.Parse(generated);
		foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
		{
			foreach (var operation in path.Value.EnumerateObject())
			{
				operation.Value.GetProperty("operationId").GetString().Should().NotBeNullOrWhiteSpace();
				operation.Value.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
			}
		}

		using var ui = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative));
		ui.StatusCode.Should().Be(HttpStatusCode.OK);
	}
	#endregion
}
