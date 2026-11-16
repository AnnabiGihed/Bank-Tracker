namespace KbcTracker.ApiService.Features.Authentication;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Decides whether a provider account is the owner: its issuer and subject must equal an entry of
///              <c>Authentication:Owners</c>. Every match maps to the one <see cref="OwnerIdentity.Id"/>.
/// </summary>
/// <param name="owners">The configured allow-list.</param>
public sealed class OwnerAllowList(IEnumerable<AllowedOwner> owners)
{
	#region Fields
	/// <summary>The normalised entries, without blank ones, which could match a missing issuer or subject.</summary>
	private readonly HashSet<(string Issuer, string Subject)> _owners = [.. owners
		.Where(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Subject))
		.Select(o => (NormalizeIssuer(o.Issuer), o.Subject.Trim()))];
	#endregion

	#region Public Methods
	/// <summary>Maps a provider account to the owner id.</summary>
	/// <param name="issuer">The provider's issuer, as the provider reported it.</param>
	/// <param name="subject">The account's subject at that provider.</param>
	/// <returns>The owner id, or <see langword="null"/> when the account is not on the allow-list.</returns>
	public string? Map(string? issuer, string? subject) =>
		string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) || !_owners.Contains((NormalizeIssuer(issuer), subject.Trim()))
			? null
			: OwnerIdentity.Id;
	#endregion

	#region Private Helpers
	/// <summary>Ignores a trailing slash, which differs between a provider's metadata and its tokens.</summary>
	private static string NormalizeIssuer(string issuer) => issuer.Trim().TrimEnd('/');
	#endregion
}
