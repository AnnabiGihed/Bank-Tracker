using Microsoft.AspNetCore.Components.WebView.Maui;

namespace KbcTracker.Mobile.Features.Shared.Shell;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The one page of the app: a <see cref="BlazorWebView"/> that loads <c>wwwroot/index.html</c> and mounts the
///              <see cref="MobileShell"/> in its <c>#app</c> element. Built in code, so the app has no XAML.
/// </summary>
public sealed class MainPage : ContentPage
{
	#region Constructors
	/// <summary>Builds the page and its web view.</summary>
	public MainPage()
	{
		var webView = new BlazorWebView { HostPage = "wwwroot/index.html" };
		webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(MobileShell) });
		Content = webView;
	}
	#endregion
}
