using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KbcTracker.Application.Features.Workspace.Contracts;
using KbcTracker.Domain.Features.Categories.Aggregates;
using KbcTracker.Domain.Features.Categories.ValueObjects;
using KbcTracker.Domain.Features.Shared.Aggregates;
using KbcTracker.Domain.Features.Transactions.Aggregates;
using KbcTracker.Domain.Features.Transactions.ValueObjects;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KbcTracker.ApiService.Tests.Features.Workspace;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies the validation and error answers of the decisions endpoint (missing fields, unknown targets,
///              invalid or valid splits, protected categories) and what it stores. These checks ran through the web host's
///              own <c>POST /api</c> relay before the components called the backend directly.
/// </summary>
/// <param name="fixture">The isolated real-database API host.</param>
public sealed class DecisionRulesTests(ApiFixture fixture) : IClassFixture<ApiFixture>, IAsyncLifetime
{
	#region Static Instances
	/// <summary>The API's JSON format, with enums as their names.</summary>
	private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
	#endregion

	#region Fields
	/// <summary>The API host of this test, used to read what was stored.</summary>
	private WebApplicationFactory<Program>? _factory;
	#endregion

	#region Theory Data
	/// <summary>Gets requests that miss a field their action needs, or carry an invalid one.</summary>
	public static TheoryData<DecisionRequest> InvalidRequests => new()
	{
		new DecisionRequest("removeFixed"),
		new DecisionRequest("txnOnly", Key: "CARD:SHOP ONE", Id: "t1"),
		new DecisionRequest("rename", Key: "CARD:SHOP ONE", Name: " "),
		new DecisionRequest("saveCategory", Name: "&&", Type: "Expense", Color: "#123456"),
	};

	/// <summary>Gets requests that point at something that does not exist.</summary>
	public static TheoryData<DecisionRequest> UnknownTargets => new()
	{
		new DecisionRequest("accept", Key: "NO:SUCH"),
		new DecisionRequest("mark", Key: "NO:SUCH"),
		new DecisionRequest("removeFixed", Id: "missing"),
		new DecisionRequest("deleteCategory", Id: "missing"),
		new DecisionRequest("updateFixed", Id: "missing", Name: "Rent"),
	};

	/// <summary>Gets splits the backend must refuse, with the expected status.</summary>
	public static TheoryData<DecisionRequest, HttpStatusCode> InvalidSplits => new()
	{
		{ Split("rules-a", Part(10, "bills"), Part(2, "cash")), HttpStatusCode.BadRequest },
		{ Split("rules-a", Part(10, "bills"), Part(2.5m, "missing")), HttpStatusCode.BadRequest },
		{ Split("rules-a", Part(10, "bills"), Part(2.5m, "income")), HttpStatusCode.BadRequest },
		{ Split("rules-a", Part(12.5m, "bills")), HttpStatusCode.BadRequest },
		{ new DecisionRequest("split", Id: "rules-a"), HttpStatusCode.BadRequest },
		{ Split("missing", Part(10, "bills"), Part(2.5m, "cash")), HttpStatusCode.NotFound },
		{ Split("rules-a", Part(10.005m, "bills"), Part(2.495m, "cash")), HttpStatusCode.BadRequest },
		{ Split("rules-income", Part(2000, "bills"), Part(500, "cash")), HttpStatusCode.BadRequest },
	};
	#endregion

	#region IAsyncLifetime Implementation
	/// <inheritdoc />
	public async Task InitializeAsync()
	{
		_factory = fixture.CreateFactory("Development");
		await ChangeAsync(async db =>
		{
			await AddCategoryIfMissingAsync(db, "bills", CategoryType.Expense).ConfigureAwait(false);
			await AddCategoryIfMissingAsync(db, "cash", CategoryType.Expense).ConfigureAwait(false);
			await AddCategoryIfMissingAsync(db, "income", CategoryType.Income).ConfigureAwait(false);
			await AddTransactionIfMissingAsync(db, "rules-a", -12.50m).ConfigureAwait(false);
			await AddTransactionIfMissingAsync(db, "rules-income", 2500m).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task DisposeAsync()
	{
		if (_factory is not null)
		{
			await _factory.DisposeAsync().ConfigureAwait(false);
		}
	}
	#endregion

	#region Validation Tests
	/// <summary>Verifies requests missing a field the action needs, or with a blank or invalid name, are refused with 400
	/// and a message instead of failing with 500.</summary>
	/// <param name="request">The decision to send.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[MemberData(nameof(InvalidRequests))]
	public async Task Decisions_MissingOrInvalidField_BadRequestWithMessage(DecisionRequest request)
	{
		using var client = fixture.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request);

		response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>Verifies actions on something that does not exist answer 404 with a message instead of failing.</summary>
	/// <param name="request">The decision to send.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[MemberData(nameof(UnknownTargets))]
	public async Task Decisions_UnknownTarget_NotFoundWithMessage(DecisionRequest request)
	{
		using var client = fixture.CreateClient();

		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
		(await response.Content.ReadAsStringAsync()).Should().NotBeNullOrWhiteSpace();
	}

	/// <summary>Verifies dismissing a suggestion stores its signature, and confirming payees through the single key or the
	/// list of keys stores their automatic category as confirmed.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_DismissAndConfirm_Stored()
	{
		using var client = fixture.CreateClient();
		var snapshot = await client.GetFromJsonAsync<WorkspaceSnapshot>(new Uri("/api/v1/workspace", UriKind.Relative), ApiJson);
		var keys = snapshot!.Analysis.Items.Select(i => i.SeriesKey).Distinct(StringComparer.Ordinal).Take(2).ToArray();

		var dismiss = await StatusAsync(client, new("dismiss", Key: "SIGNATURE:RULES"));
		var confirmOne = await StatusAsync(client, new("confirm", Key: keys[0]));
		var confirmAll = await StatusAsync(client, new("confirm", Keys: keys));

		dismiss.Should().Be(HttpStatusCode.OK);
		confirmOne.Should().Be(HttpStatusCode.OK);
		confirmAll.Should().Be(HttpStatusCode.OK);
		var user = await LoadUserDataAsync();
		user.DismissedSuggestions.Should().Contain("SIGNATURE:RULES");
		user.Payees.Where(p => keys.Contains(p.Key)).Should().HaveCount(keys.Length).And.OnlyContain(p => p.Value.Confirmed);
	}
	#endregion

	#region Split Tests
	/// <summary>Verifies splitting a payment stores its trimmed parts, and removing the split brings the payment back to
	/// its own category, once.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_SplitThenUnsplit_PartsStoredAndRemoved()
	{
		const string payment = "rules-unsplit-payment";
		await ChangeAsync(db => AddTransactionIfMissingAsync(db, payment, -12.50m));
		using var client = fixture.CreateClient();

		var split = await StatusAsync(client, Split(payment, Part(10, "bills", " Water "), Part(2.50m, "cash")));
		var stored = (await LoadUserDataAsync()).Splits[new TxnId(payment)].Lines.Select(l => (l.CategoryId.Value, l.Amount, l.Label)).ToList();
		var unsplit = await StatusAsync(client, new("unsplit", Id: payment));
		var unsplitAgain = await StatusAsync(client, new("unsplit", Id: payment));

		split.Should().Be(HttpStatusCode.OK);
		stored.Should().Equal(("bills", 10m, "Water"), ("cash", 2.50m, (string?)null));
		unsplit.Should().Be(HttpStatusCode.OK);
		unsplitAgain.Should().Be(HttpStatusCode.NotFound);
		(await LoadUserDataAsync()).Splits.Should().NotContainKey(new TxnId(payment));
	}

	/// <summary>Verifies a split that does not add up, uses an unknown or non-spending category or has one part is refused,
	/// and that splitting an unknown payment answers 404; nothing is stored.</summary>
	/// <param name="request">The split to send.</param>
	/// <param name="status">The expected answer.</param>
	/// <returns>A task that completes when verified.</returns>
	[Theory]
	[MemberData(nameof(InvalidSplits))]
	public async Task Decisions_InvalidSplit_RefusedAndNothingStored(DecisionRequest request, HttpStatusCode status)
	{
		using var client = fixture.CreateClient();

		var answer = await StatusAsync(client, request);

		answer.Should().Be(status);
		(await LoadUserDataAsync()).Splits.Should().NotContainKey(new TxnId("rules-a")).And.NotContainKey(new TxnId("rules-income"));
	}

	/// <summary>Verifies deleting a category moves the split lines that use it to its parent, and that a category used by a
	/// split cannot stop counting as spending.</summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Decisions_CategoryOfSplitLine_LineMovedToParentAndTypeKept()
	{
		const string payment = "rules-category-payment";
		await ChangeAsync(db => AddTransactionIfMissingAsync(db, payment, -12.50m));
		using var client = fixture.CreateClient();
		var pets = await StatusAsync(client, new("saveCategory", Name: "Rules pets tree", Type: "Expense", Color: "#123456", Keywords: ""));
		var vet = await StatusAsync(client, new("saveCategory", Name: "Rules vet tree", Type: "Expense", Color: "#123456", Keywords: "", ParentId: "rules-pets-tree"));
		var split = await StatusAsync(client, Split(payment, Part(10, "rules-vet-tree"), Part(2.50m, "bills")));

		var toSavings = await StatusAsync(client, new("saveCategory", Id: "rules-pets-tree", Name: "Rules pets tree", Type: "Savings", Color: "#123456", Keywords: ""));
		var rename = await StatusAsync(client, new("saveCategory", Id: "rules-pets-tree", Name: "Rules animals tree", Type: "Expense", Color: "#123456", Keywords: ""));
		var delete = await StatusAsync(client, new("deleteCategory", Id: "rules-vet-tree"));

		new[] { pets, vet, split, rename, delete }.Should().AllBeEquivalentTo(HttpStatusCode.OK);
		toSavings.Should().Be(HttpStatusCode.BadRequest);
		(await LoadUserDataAsync()).Splits[new TxnId(payment)].Lines.Select(l => l.CategoryId.Value).Should().Equal("rules-pets-tree", "bills");
	}
	#endregion

	#region Private Helpers
	/// <summary>Builds a split request.</summary>
	private static DecisionRequest Split(string id, params SplitPartRequest[] parts) => new("split", Id: id, Parts: parts);

	/// <summary>Builds one part of a split request.</summary>
	private static SplitPartRequest Part(decimal amount, string categoryId, string? label = null) => new(amount, categoryId, label);

	/// <summary>Posts a decision and returns only the HTTP status.</summary>
	private static async Task<HttpStatusCode> StatusAsync(HttpClient client, DecisionRequest request)
	{
		using var response = await client.PostAsJsonAsync(new Uri("/api/v1/decisions", UriKind.Relative), request).ConfigureAwait(false);
		return response.StatusCode;
	}

	/// <summary>Reads the stored user decisions through a fresh database scope.</summary>
	private async Task<UserData> LoadUserDataAsync()
	{
		var scope = (_factory ?? throw new InvalidOperationException("The API is not started.")).Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			return await scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>().UserData.AsNoTracking().SingleAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Stores test setup in its own scope.</summary>
	private async Task ChangeAsync(Func<KbcTrackerDbContext, Task> change)
	{
		var scope = (_factory ?? throw new InvalidOperationException("The API is not started.")).Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			var db = scope.ServiceProvider.GetRequiredService<KbcTrackerDbContext>();
			await change(db).ConfigureAwait(false);
			await db.SaveChangesAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Adds a category the splits need unless another test of this class added it already; these shared ones are never changed.</summary>
	private static async Task AddCategoryIfMissingAsync(KbcTrackerDbContext db, string id, CategoryType type)
	{
		if (!await db.Categories.AnyAsync(c => c.Id == new CategoryId(id)).ConfigureAwait(false))
		{
			db.Categories.Add(Category.Rehydrate(new CategoryId(id), id, type, "#123456", [], builtIn: false));
		}
	}

	/// <summary>Adds a payment of the test account unless an earlier test already did.</summary>
	private static async Task AddTransactionIfMissingAsync(KbcTrackerDbContext db, string id, decimal amount)
	{
		if (!await db.Transactions.AnyAsync(t => t.Id == new TxnId(id)).ConfigureAwait(false))
		{
			db.Transactions.Add(Txn.Rehydrate(new TxnId(id), "current", new DateOnly(2026, 10, 3), Money.Rehydrate(amount, "EUR"),
				Counterparty.Rehydrate("Rules Shop", null), "Payment", BankRecord.None));
		}
	}
	#endregion
}
