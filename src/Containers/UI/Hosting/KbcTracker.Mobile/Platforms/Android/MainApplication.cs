using Android.App;
using Android.Runtime;

namespace KbcTracker.Mobile;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The Android application class. Its <c>[Application]</c> registration, which differs between Debug
///              and Release, is in MainApplication.Registration.cs.
/// </summary>
public sealed partial class MainApplication : MauiApplication
{
	#region Constructors
	/// <summary>Creates the application for the runtime.</summary>
	/// <param name="handle">The native handle of the application.</param>
	/// <param name="ownership">How the handle is owned.</param>
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}
	#endregion

	#region Protected Methods
	/// <inheritdoc />
	protected override MauiApp CreateMauiApp()
	{
		return MauiProgram.CreateMauiApp();
	}
	#endregion
}
