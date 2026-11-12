using KbcTracker.Domain.Features.Categories.Services;
using KbcTracker.Persistence.EntityFrameworkCore.Features.Categories;
using Pivot.Framework.Domain.Shared;

namespace KbcTracker.Persistence.EntityFrameworkCore.Tests.Features.Categories;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies <see cref="CategoryTreeUpgrade"/> on a real PostgreSQL database: the old flat default list,
///              with decisions that point to the two defaults that disappear, becomes the category tree; what the
///              owner edited or created is kept; and a second run, or a database that already has a tree, changes
///              nothing.
/// </summary>
/// <param name="fixture">The shared PostgreSQL container.</param>
public sealed class CategoryTreeUpgradeTests(PostgreSqlFixture fixture) : DatabaseTests(fixture)
{
	#region Upgrade Tests
	/// <summary>
	/// Verifies that the flat default set becomes the tree: missing nodes are added, kept ids are attached and take the
	/// new name, colour and keywords while untouched, an edited one keeps its own, and the owner's own category stays
	/// top level.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_FlatDefaults_BecomeTreeKeepingOwnerChoices()
	{
		await AddAsync([.. FlatDefaults(editedCar: true), OwnerCategory(), Decisions()]);

		var result = await UpgradeAsync();

		result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "");
		result.Value.Should().BeTrue();
		var categories = await ReadAsync(db => db.Categories.ToListAsync());
		categories.Should().OnlyHaveUniqueItems(c => c.Id);
		categories.Select(c => c.Id.Value).Should().Contain(CategoryDefaults.CreateDefaults("BE").Select(c => c.Id.Value));
		categories.Should().NotContain(c => c.Id == new CategoryId("subscriptions") || c.Id == new CategoryId("utilities-telecom"));

		categories.Should().NotContain(c => c.Id == new CategoryId("transport-fuel"));
		var fuel = categories.Single(c => c.Id == new CategoryId("car-fuel"));
		fuel.Name.Should().Be("Fuel & charging");
		fuel.ParentId.Should().Be(new CategoryId("car"));
		fuel.Coicop.Should().Be("07.2");
		fuel.Keywords.Should().Contain(["SHELL", "DATS 24"]);

		var untouched = categories.Single(c => c.Id == new CategoryId("groceries"));
		untouched.Name.Should().Be("Food & non-alcoholic drinks");
		untouched.ParentId.Should().BeNull();
		untouched.Keywords.Should().Contain(["LIDL", "DELHAIZE"]);

		var edited = categories.Single(c => c.Id == new CategoryId("car"));
		edited.Name.Should().Be("My car");
		edited.Keywords.Should().Equal("MYGARAGE");
		edited.Color.Should().Be("#123456");
		edited.ParentId.Should().Be(new CategoryId("transport-own"));
		edited.Coicop.Should().Be("07.2");

		var owner = categories.Single(c => c.Id == new CategoryId("hobby"));
		owner.ParentId.Should().BeNull();
		owner.Keywords.Should().Equal("KNITTING");
	}

	/// <summary>
	/// Verifies that every reference to a merged flat default moves to the category whose keyword matches its payee
	/// (payee rules by series key, payment choices by the transaction's shop, fixed payments by name), to the fallback
	/// node when none matches, that payee rules become unconfirmed and that payment choices keep their state.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_DecisionsOnMergedDefaults_MovedByKeywordWithFallback()
	{
		await AddAsync([.. FlatDefaults(editedCar: false), Decisions(),
			Card("t1", "ORANGE"), Card("t2", "STIB"), Card("t3", "MYSTERY SHOP")]);

		(await UpgradeAsync()).IsSuccess.Should().BeTrue();

		var user = await ReadAsync(db => db.UserData.SingleAsync());
		user.Payees["CARD:NETFLIX"].Should().Be(new PayeeAssignment(new CategoryId("rec-streaming"), Confirmed: false));
		user.Payees["CARD:SHELL"].Should().Be(new PayeeAssignment(new CategoryId("car-fuel"), Confirmed: false));
		user.Payees["CARD:PROXIMUS"].Should().Be(new PayeeAssignment(new CategoryId("comm-services"), Confirmed: false));
		user.Payees["CARD:NOTHING KNOWN"].Should().Be(new PayeeAssignment(new CategoryId("transport"), Confirmed: false));
		user.Payees["CARD:UNKNOWN PHONE"].Should().Be(new PayeeAssignment(new CategoryId("bills"), Confirmed: false));
		user.TxnCategories[new TxnId("t1")].Should().Be(new CategoryId("comm-services"));
		user.TxnCategories[new TxnId("t2")].Should().Be(new CategoryId("transport-public"));
		user.TxnCategories[new TxnId("t3")].Should().Be(new CategoryId("bills"));
		user.Recurring.Single().CategoryId.Should().Be(new CategoryId("rec-streaming"));
		user.Recurring.Single().Active.Should().BeTrue();
	}

	/// <summary>
	/// Verifies that the keywords of a merged category the owner edited are added, without duplicates, to the node that
	/// takes over, and that they then route the owner's payees there.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_EditedMergedCategory_KeywordsAppendedToFallback()
	{
		var edited = Category.Rehydrate(new CategoryId("transport-fuel"), "My fuel", CategoryType.Expense, "#111111", ["MYPUMP", "SHELL"], builtIn: false);
		var flat = FlatDefaults(editedCar: false).Where(e => e is not Category c || c.Id != new CategoryId("transport-fuel")).ToList();
		var user = UserData.Rehydrate([], [], [KeyValuePair.Create("CARD:MYPUMP", new PayeeAssignment(new CategoryId("transport-fuel"), Confirmed: true))], [], []);
		await AddAsync([.. flat, edited, user]);

		(await UpgradeAsync()).IsSuccess.Should().BeTrue();

		var categories = await ReadAsync(db => db.Categories.ToListAsync());
		var transport = categories.Single(c => c.Id == new CategoryId("transport"));
		transport.Keywords.Should().Contain(["MYPUMP", "SHELL"]).And.OnlyHaveUniqueItems();
		(await ReadAsync(db => db.UserData.SingleAsync())).Payees["CARD:MYPUMP"].CategoryId.Should().Be(new CategoryId("transport"));
	}

	/// <summary>
	/// Verifies that running the upgrade a second time changes nothing.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_SecondRun_ChangesNothing()
	{
		await AddAsync([.. FlatDefaults(editedCar: false), Decisions()]);
		await UpgradeAsync();
		var before = await ReadAsync(db => db.Categories.OrderBy(c => c.Id).Select(c => new { c.Id, c.Name, c.ParentId }).ToListAsync());

		var second = await UpgradeAsync();

		second.IsSuccess.Should().BeTrue();
		second.Value.Should().BeFalse();
		(await ReadAsync(db => db.Categories.OrderBy(c => c.Id).Select(c => new { c.Id, c.Name, c.ParentId }).ToListAsync())).Should().Equal(before);
	}

	/// <summary>
	/// Verifies that a database that already has a tree is left alone, even when the owner deleted a default.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_TreeAlreadyPresent_Untouched()
	{
		var tree = CategoryDefaults.CreateDefaults("BE").Where(c => c.Id != new CategoryId("car-parking")).ToList();
		await AddAsync([.. tree, UserData.Empty()]);

		var result = await UpgradeAsync();

		result.Value.Should().BeFalse();
		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(tree.Count);
	}

	/// <summary>
	/// Verifies that a tree the owner flattened after the upgrade is not seeded again: none of the merged flat
	/// defaults exists any more, so the upgrade has nothing to do.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_TreeFlattenedByOwner_NotSeededAgain()
	{
		await AddAsync([.. FlatDefaults(editedCar: false), Decisions()]);
		await UpgradeAsync();
		await SaveAsync(async db =>
		{
			foreach (var category in await db.Categories.ToListAsync().ConfigureAwait(false))
			{
				category.Update(category.Name, category.Type, category.Color, category.Keywords, parentId: null).IsSuccess.Should().BeTrue();
			}

			db.Categories.Remove(await db.Categories.SingleAsync(c => c.Id == new CategoryId("car-parking")).ConfigureAwait(false));
		});
		var before = await ReadAsync(db => db.Categories.CountAsync());

		var result = await UpgradeAsync();

		result.Value.Should().BeFalse();
		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(before);
	}

	/// <summary>
	/// Verifies that a database that was not initialised, with no decisions row, is not given categories.
	/// </summary>
	/// <returns>A task that completes when verified.</returns>
	[Fact]
	public async Task Upgrade_NotInitialised_Untouched()
	{
		var result = await UpgradeAsync();

		result.Value.Should().BeFalse();
		(await ReadAsync(db => db.Categories.CountAsync())).Should().Be(0);
	}
	#endregion

	#region Private Helpers
	/// <summary>
	/// Runs the upgrade in its own scope, as the initialisation command does.
	/// </summary>
	private async Task<Result<bool>> UpgradeAsync()
	{
		var scope = Services.CreateAsyncScope();
		await using (scope.ConfigureAwait(false))
		{
			return await scope.ServiceProvider.GetRequiredService<CategoryTreeUpgrade>().UpgradeAsync("BE", CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Builds the old flat default list as it was seeded; the first ten are the built-in categories.
	/// </summary>
	/// <param name="editedCar">Whether the owner renamed the car category and changed its colour and keywords.</param>
	private static List<object> FlatDefaults(bool editedCar) =>
	[
		.. LegacyFlatCategories.ById.Values.Select((e, index) => editedCar && string.Equals(e.Id, "car", StringComparison.Ordinal)
			? Category.Rehydrate(new CategoryId(e.Id), "My car", e.Type, "#123456", ["MYGARAGE"], builtIn: false)
			: Category.Rehydrate(new CategoryId(e.Id), e.Name, e.Type, e.Color, e.Keywords, builtIn: index < 10)),
	];

	/// <summary>
	/// A category the owner created, which is in neither the old seed nor the tree.
	/// </summary>
	private static Category OwnerCategory() =>
		Category.Rehydrate(new CategoryId("hobby"), "Hobby", CategoryType.Expense, "#abcdef", ["KNITTING"], builtIn: false);

	/// <summary>
	/// A card payment of the test account in October 2026.
	/// </summary>
	private static Txn Card(string id, string shop) => Txn.Rehydrate(
		new TxnId(id), "acc-1", new DateOnly(2026, 10, 5), Money.Rehydrate(-10m, "EUR"), Counterparty.Rehydrate(shop, null),
		$"{shop} BE1000 Bruxelles Payment with card", new BankRecord("PMNT", """{"entry_reference":"x"}"""));

	/// <summary>
	/// Decisions that point to the three flat defaults that are merged: payee rules on all three, one matching a
	/// keyword of the new tree and one not, payment choices on <c>utilities-telecom</c>, <c>transport-fuel</c> and
	/// <c>utilities-telecom</c> again for a shop no keyword knows, and a fixed payment on <c>subscriptions</c>.
	/// </summary>
	private static UserData Decisions()
	{
		var payment = RecurringPayment.Rehydrate(
			new RecurringPaymentId("netflix"),
			"Netflix",
			new CategoryId("subscriptions"),
			new PayeeMatch(["CARD:NETFLIX"], ["NETFLIX"]),
			new RecurrenceRule(13.99m, 15m, 30),
			active: true,
			Samples.BrusselsMorning);
		return UserData.Rehydrate(
			[payment],
			[],
			[
				KeyValuePair.Create("CARD:NETFLIX", new PayeeAssignment(new CategoryId("subscriptions"), Confirmed: true)),
				KeyValuePair.Create("CARD:SHELL", new PayeeAssignment(new CategoryId("transport-fuel"), Confirmed: true)),
				KeyValuePair.Create("CARD:PROXIMUS", new PayeeAssignment(new CategoryId("utilities-telecom"), Confirmed: true)),
				KeyValuePair.Create("CARD:NOTHING KNOWN", new PayeeAssignment(new CategoryId("transport-fuel"), Confirmed: true)),
				KeyValuePair.Create("CARD:UNKNOWN PHONE", new PayeeAssignment(new CategoryId("utilities-telecom"), Confirmed: true)),
			],
			[
				KeyValuePair.Create(new TxnId("t1"), new CategoryId("utilities-telecom")),
				KeyValuePair.Create(new TxnId("t2"), new CategoryId("transport-fuel")),
				KeyValuePair.Create(new TxnId("t3"), new CategoryId("utilities-telecom")),
			],
			[]);
	}
	#endregion
}
