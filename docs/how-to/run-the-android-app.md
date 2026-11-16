# Run the Android app

The Android app shows the same tabs as the web app, built from the same components
([ADR-0008](../adr/0008-build-the-front-ends-as-shared-blazor-components.md)). For now it runs only in an Android
emulator on your PC, against the backend on the same PC. A physical phone has to wait until the API runs on a server
with authentication.

## Prerequisites

- The .NET 10 SDK and the private-feed credentials described in the root README.
- The Android workload: `dotnet workload install android`.
- A JDK and the Android SDK. Visual Studio's mobile workload or Android Studio installs both.
- An emulator running Android 10 (API 29) or later, created in the Android Device Manager.
- A backend you can start, set up as in [Run the backend](run-the-backend.md), with sign-in providers configured and
  your account on the owner list.
- `adb` from the Android SDK platform tools, on your path.

## Run the app

1. Start the backend on your PC, with the AppHost or on its own. It listens on `http://127.0.0.1:5080`.
1. Start the emulator from the Android Device Manager and wait until it shows the home screen.
1. Tunnel the emulator's `localhost:5080` to the backend on your PC. Run it again after each emulator start:

   ```powershell
   adb reverse tcp:5080 tcp:5080
   ```

1. Build, install and start the app:

   ```powershell
   dotnet build src/Containers/UI/Hosting/KbcTracker.Mobile -t:Run -f net10.0-android
   ```

The Debug build uses `http://localhost:5080/` as the backend address, which `adb reverse` forwards to your PC. It has
to be `localhost` because Google accepts a plain-HTTP redirect for `localhost` only. See
[Configuration](../reference/configuration.md#android-app). Cleartext HTTP is allowed for `localhost` and `127.0.0.1`
in Debug builds only.

## Sign in

1. The app opens on a sign-in screen with one button per provider. Tap "Sign in with Google".
1. The system browser opens. Sign in with the owner's account, which the backend's owner list names.
1. The browser returns to the app (`com.banktracker.mobile://auth`) and the tabs load.

The app keeps only a refresh token, in Android's secure storage, so you stay signed in after a restart. "Sign out" in
the header revokes it at the backend and returns to the sign-in screen. When the backend ends the session (you
signed out elsewhere, or your account left the owner list), the app asks you to sign in again.

## What works

- Every tab: Overview, Fixed payments, Transactions, To review, Categories and Settings.
- Your decisions: categories, confirmations, fixed payments and splits.
- Sync, from the header, the banner or Settings.

## What does not work

- Connecting or reconnecting the bank. The app tells you to use the web app on your PC.
- The CSV export. Use the web app.
- A physical phone. The API has no production address yet.

## Troubleshooting

**NETSDK1147 when you build.** The Android workload needs Mono packs that match the SDK. Update the workloads, then
build again:

```powershell
dotnet workload update
```

**The app shows the backend as unavailable, or sign-in says it cannot reach the backend.** Check that the backend runs
and answers at <http://127.0.0.1:5080>, that you ran `adb reverse tcp:5080 tcp:5080` after starting the emulator, and
that you started the Debug build.

**The browser says the account may not use KbcTracker.** The account you chose is not on the backend's owner list.

**You do not have Android installed.** Build and test everything else with `dotnet build KbcTracker.Core.slnf`. See
[Run the tests](run-the-tests.md).
