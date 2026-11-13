namespace KbcTracker.Components.Tests.Features.Shared;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The bUnit context of every component test: Radzen's JavaScript calls are answered loosely (Radzen needs
///              them for dialogs and drop-downs, and a test does not care), the shared UI services are registered the way a
///              host registers them, and the backend is a stub that serves an invented workspace.
/// </summary>
public abstract class UiTestContext : BunitContext
{
	#region Fields
	/// <summary>The stub that stands in for the backend's HTTP transport.</summary>
	private readonly StubBackend _backend = new();
	#endregion

	#region Constructors
	/// <summary>Initialises a new <see cref="UiTestContext"/>.</summary>
	protected UiTestContext()
	{
		JSInterop.Mode = JSRuntimeMode.Loose;
		JSInterop.Setup<Radzen.Blazor.Rendering.Rect>("Radzen.createChart", _ => true).SetResult(new Radzen.Blazor.Rendering.Rect { Width = 600, Height = 300 });
		Launcher = Substitute.For<IBankConnectionLauncher>();
		Launcher.CanLaunch.Returns(true);
		Services.AddKbcTrackerUi(ServiceLifetime.Scoped, options => options.ExportCsvPath = "/export.csv");
		Services.AddSingleton(_backend.Client());
		Services.AddSingleton(Launcher);
	}
	#endregion

	#region Properties
	/// <summary>Gets the stub backend, to configure answers and to see what was sent.</summary>
	internal StubBackend Backend => _backend;

	/// <summary>Gets the launcher the pages use to open the bank; it can launch unless a test says otherwise.</summary>
	protected IBankConnectionLauncher Launcher { get; }
	#endregion

	#region Protected Methods
	/// <summary>Renders the host components of Radzen (dialogs and notifications), which a page needs to show them.</summary>
	/// <returns>The rendered host, to look into the open dialog or the notifications.</returns>
	protected IRenderedComponent<RadzenComponents> RenderRadzenHost() => Render<RadzenComponents>();

	/// <summary>Makes the backend serve a workspace.</summary>
	/// <param name="analysis">The analysis to serve.</param>
	/// <param name="connected">Whether the bank is connected.</param>
	protected void Serve(Analysis analysis, bool connected = true) => _backend.ServeWorkspace(analysis, connected);

	/// <summary>Selects the month the pages show, as the layout does from <c>?m=</c>; without it the newest month (the current one) shows.</summary>
	/// <param name="month">The month as <c>yyyy-MM</c>.</param>
	protected void SelectMonth(string month) => Services.GetRequiredService<WorkspaceStore>().SelectMonth(month);

	/// <summary>Finds a button by its visible text.</summary>
	/// <typeparam name="T">The type of the rendered component.</typeparam>
	/// <param name="container">What to look into.</param>
	/// <param name="text">The text of the button.</param>
	/// <returns>The button.</returns>
	protected static AngleSharp.Dom.IElement ButtonWithText<T>(IRenderedComponent<T> container, string text)
		where T : IComponent =>
		container.FindAll("button").First(b => string.Equals(b.TextContent.Trim(), text, StringComparison.Ordinal));
	#endregion

	#region Overrides
	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_backend.Dispose();
		}

		base.Dispose(disposing);
	}
	#endregion
}
