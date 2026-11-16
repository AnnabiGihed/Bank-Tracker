using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Web;

namespace KbcTracker.Web.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The HTML shell of the web app, rendered statically on every request: the style bundle, the Blazor and
///              Radzen scripts and the interactive shell. It is the only place that still sees the request, so it reads the
///              one-time <see cref="PageNotice"/> cookie here (which also clears it) and passes the message on, and creates the
///              antiforgery token of the sign-out button, which the interactive shell cannot make itself.
/// </summary>
public sealed partial class App : ComponentBase
{
	#region Static Instances
	/// <summary>Interactive Server without prerendering: the workspace is loaded once, by the circuit, not twice.</summary>
	private static readonly InteractiveServerRenderMode InteractiveRender = new(prerender: false);
	#endregion

	#region Fields
	/// <summary>The message of a failed action that redirected here, or <see langword="null"/> for none.</summary>
	private string? _notice;

	/// <summary>The label of the signed-in account.</summary>
	private string _accountName = string.Empty;

	/// <summary>The name of the antiforgery form field of the sign-out post.</summary>
	private string _tokenField = string.Empty;

	/// <summary>The antiforgery token of the sign-out post.</summary>
	private string _token = string.Empty;
	#endregion

	#region Injected Services
	/// <summary>Gets or sets the antiforgery service that creates the sign-out token.</summary>
	[Inject]
	public required IAntiforgery Antiforgery { get; set; }
	#endregion

	#region Parameters
	/// <summary>Gets or sets the request of this page, available only while the shell is rendered statically.</summary>
	[CascadingParameter]
	public HttpContext? HttpContext { get; set; }
	#endregion

	#region Lifecycle Methods
	/// <inheritdoc />
	protected override void OnInitialized()
	{
		if (HttpContext is null)
		{
			return;
		}

		_notice = PageNotice.Take(HttpContext);
		var tokens = Antiforgery.GetAndStoreTokens(HttpContext);
		_tokenField = tokens.FormFieldName;
		_token = tokens.RequestToken ?? string.Empty;
		_accountName = HttpContext.User.FindFirst("name")?.Value ?? "Owner";
	}
	#endregion
}
