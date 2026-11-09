# ADR-0001: Build the tracker as a .NET MAUI Android app with a shared core and SQLite

- Status: Superseded by ADR-0003
- Date: 2026-11-09
- Deciders: Gihed Annabi

## Context

KbcTracker started as a local ASP.NET Core web app on a Windows PC. It reads one KBC Brussels current account
through Enable Banking (PSD2, free restricted mode), keeps the history in JSON files under `%LOCALAPPDATA%`, and
seeds its categories from `appsettings.json`.

The user now wants:

- an app installed on an Android phone, with notifications (a fixed payment changed amount or is late, a new payee
  is not categorised yet);
- the data, including the categories, in a database that works on Android, not in `appsettings.json`;
- more detection (salary versus other income, quarterly and yearly payments), Loan and Salary tabs, and the savings
  account.

Forces:

- KBC returns only about 90 days of transactions, so the stored history must never be lost or deleted.
- Every Enable Banking call is signed with the application's private key. Wherever the sync runs, the key must be
  there.
- The detection logic (`Analysis`) is the valuable part and has no tests. It must not be rewritten for a new
  platform.
- The project avoids NuGet packages unless there is a strong reason.
- Single user, personal use, read-only bank access.

Options considered:

1. A native .NET MAUI Android app: sync, database and notifications on the phone.
2. An installable web app (PWA) served by the existing server, hosted somewhere, with web push.
3. A MAUI client on the phone with a home server that keeps the key, syncs and stores the data.

## Decision

We build a native .NET MAUI Android app on .NET 10. The bank sync, the SQLite database and the notifications run on
the phone. The private key is imported once into Android secure storage; it is never committed and never packaged in
the app.

We move the platform-neutral code into a class library, `src/KbcTracker.Core` (models, `Analysis`, the Enable
Banking client and mapper, `SyncService`, the default categories and the `IStore` storage contract). The web host and
the Android app both use it. The core must not reference ASP.NET Core or MAUI.

We store data in SQLite through `Microsoft.Data.Sqlite`, behind `IStore`. The categories live in the database. The
JSON files are imported once.

We keep the web host, in `src/KbcTracker.Web` on .NET 10, until the Android app works end to end, then retire it.

We test the core with MSTest in `tests/KbcTracker.Core.Tests`. The tests pin today's detection (classification,
categories, the YouTube, rent and loan suggestions) before it is changed.

These NuGet packages are accepted as exceptions to the "no packages" rule:

- `Microsoft.Extensions.Logging.Abstractions`, so the core's `SyncService` can log through both hosts.
- `MSTest`, for the tests.
- `Microsoft.Data.Sqlite`, for the database (step 2).
- The MAUI and AndroidX packages the Android app needs (from step 5).

Delivery follows these steps, each on its own branch:

1. Shared core and tests.
1. SQLite store and JSON import.
1. Several accounts, including savings.
1. Detection: salary, quarterly and yearly payments, Loan and Salary views.
1. MAUI shell with read-only tabs.
1. Bank consent and key import on the phone.
1. Background sync and notifications.
1. Retire the web host.

## Consequences

**Positive**

- One implementation of the detection logic, tested once, used by both hosts during the move.
- No server to host or secure, and no PC that must stay on: the phone syncs and notifies by itself.
- Real Android notifications and an app that works offline on the stored history.
- Categories and all user decisions live in one database instead of being split between files and configuration.

**Negative**

- The private key lives on the phone. A lost phone means revoking the key in the Enable Banking Control Panel and
  registering a new one.
- The bank consent must redirect back into the app. Enable Banking's documentation does not say whether a custom
  URL scheme is accepted. If it is not, the redirect needs a small static https page that hands the code to the app.
  The itsme app switch on Android must be tested on the device.
- Android limits background work: syncs run at most every few hours and not at an exact time. PSD2 may also limit
  how often the bank can be read without the user present.
- MAUI 10 is supported until 2027-05-11, so moving to the next .NET version is planned work.
- New packages, and a larger build: the Android workload and SDK are needed to build the app.
- Moving the web host to .NET 10 means the PC needs the .NET 10 SDK and runtime.

## Alternatives considered

- **Installable web app with a hosted server** - rejected. It needs hosting, a login and web push. The user prefers
  an app on the phone with no server to run.
- **MAUI client with a home server** - rejected. It is the safest place for the key, but the PC must stay on and be
  reachable from the phone, which brings back hosting and networking work.
- **Entity Framework Core or sqlite-net-pcl instead of `Microsoft.Data.Sqlite`** - rejected. The schema has about ten
  small tables; an ORM and its migration tooling add weight without benefit. `Microsoft.Data.Sqlite` is first-party
  and bundles the same SQLite version on every platform.
- **Keep the JSON files on the phone** - rejected. The user asked for a database, and the categories must not live
  in configuration.
