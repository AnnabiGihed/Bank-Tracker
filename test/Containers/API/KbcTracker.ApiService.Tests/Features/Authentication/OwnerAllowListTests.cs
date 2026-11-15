using KbcTracker.ApiService.Features.Authentication;

namespace KbcTracker.ApiService.Tests.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Verifies how a provider account is mapped to the one owner id: an exact issuer and subject on the allow-list
///              maps, everything else, including blank values and a different provider's identical subject, does not.
/// </summary>
public sealed class OwnerAllowListTests
{
	#region Constants
	/// <summary>The issuer of the configured entry.</summary>
	private const string Google = "https://accounts.google.com";
	#endregion

	#region Mapping Tests
	/// <summary>Verifies a listed account maps to the owner id, and a trailing slash on the issuer does not matter.</summary>
	/// <param name="issuer">The issuer reported by the provider.</param>
	[Theory]
	[InlineData(Google)]
	[InlineData(Google + "/")]
	public void Map_ListedAccount_OwnerId(string issuer)
	{
		var list = new OwnerAllowList([new AllowedOwner { Issuer = Google, Subject = "1234567890" }]);

		list.Map(issuer, "1234567890").Should().Be(OwnerIdentity.Id);
	}

	/// <summary>Verifies blanks around the incoming subject do not matter.</summary>
	[Fact]
	public void Map_SubjectWithSpaces_OwnerId()
	{
		var list = new OwnerAllowList([new AllowedOwner { Issuer = Google, Subject = "1234567890" }]);

		list.Map(Google, " 1234567890 ").Should().Be(OwnerIdentity.Id);
	}

	/// <summary>Verifies an account that is not listed, or one whose subject matches at another provider, is not mapped.</summary>
	/// <param name="issuer">The issuer reported by the provider.</param>
	/// <param name="subject">The subject reported by the provider.</param>
	[Theory]
	[InlineData(Google, "999")]
	[InlineData("https://login.microsoftonline.com/tenant/v2.0", "1234567890")]
	[InlineData(Google, "")]
	[InlineData("", "1234567890")]
	[InlineData(null, null)]
	public void Map_UnlistedOrIncomplete_Null(string? issuer, string? subject)
	{
		var list = new OwnerAllowList([new AllowedOwner { Issuer = Google, Subject = "1234567890" }]);

		list.Map(issuer, subject).Should().BeNull();
	}

	/// <summary>Verifies an empty allow-list, and entries with a blank half, let nobody in: a blank entry must not match a blank claim.</summary>
	[Fact]
	public void Map_EmptyOrBlankEntries_NobodyMaps()
	{
		var list = new OwnerAllowList([new AllowedOwner(), new AllowedOwner { Issuer = Google }, new AllowedOwner { Subject = "1" }]);

		list.Map(Google, "1").Should().BeNull();
		list.Map(string.Empty, string.Empty).Should().BeNull();
		new OwnerAllowList([]).Map(Google, "1").Should().BeNull();
	}

	/// <summary>Verifies several listed accounts, for example one per provider, all map to the same single owner id.</summary>
	[Fact]
	public void Map_SeveralAccounts_SameOwnerId()
	{
		var list = new OwnerAllowList(
		[
			new AllowedOwner { Issuer = Google, Subject = "a" },
			new AllowedOwner { Issuer = "https://appleid.apple.com", Subject = "b" },
		]);

		list.Map(Google, "a").Should().Be(list.Map("https://appleid.apple.com", "b"));
	}
	#endregion
}
