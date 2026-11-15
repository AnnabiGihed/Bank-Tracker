using Android.App;

namespace KbcTracker.Mobile;

#if DEBUG
/// <content>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Debug registration of the Android application: it allows cleartext HTTP to <c>localhost</c> and
///              <c>127.0.0.1</c> only, through the network security config, so the app reaches the PC's API through
///              <c>adb reverse</c>.
/// </content>
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
public sealed partial class MainApplication;
#else
/// <content>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : Release registration of the Android application: it references no network security config, so
///              Android blocks cleartext HTTP.
/// </content>
[Application]
public sealed partial class MainApplication;
#endif
