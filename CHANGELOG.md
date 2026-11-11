# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Breaking changes

- The web app no longer runs on its own: start the backend first, after setting up its PostgreSQL database and
  importing your JSON data folder once with `--import-json`
  (see [Run the backend](docs/how-to/run-the-backend.md)).
- The web app's `--sync` option, its automatic sync and its `DataDirectory`, `AutoSyncHours`, `EnableBanking` and
  `Categories` settings are gone; configure the backend instead
  (see [Configuration](docs/reference/configuration.md)).
- The daily scheduled task from `scripts/install-daily-sync.ps1` no longer syncs; remove it
  (see [Keep syncing every day](docs/how-to/sync-every-day.md)).

### Added

- A backend, `KbcTracker.ApiService`, that keeps your history and decisions in PostgreSQL and owns the bank key
  and the sync ([ADR-0004](docs/adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md), proposed). Its
  API serves the history, categories, the analysis the pages show, your decisions, the bank consent, the
  connection status and manual sync; [docs/api/openapi.json](docs/api/openapi.json) describes it. Outside
  Development only a loopback caller with the owner's Tailscale identity gets in.
- The backend syncs by itself while it runs, every `AutoSyncHours`, and a reconnect is followed at once by a sync
  of the whole history the bank still holds. A sync is also full whenever the last successful one is more than
  30 days old, or when an account has no history yet (for example one added at a reconnect), so a consent gap,
  a failed sync or a new account no longer leaves days that are never downloaded.
- Backend maintenance commands: `--migrate`, `--import-json <folder>` to bring over the JSON data folder of an
  older version (only read, never changed), `--initialize` to start fresh, and `--sync`. Until the database is
  imported or initialised, decisions and new bank connections are refused so that the import stays possible.
- Documentation in `docs/`: a get-started tutorial, how-to guides, reference pages for the configuration, tabs and
  data folder, and explanations of the classification and the architecture. Each project has a `README.md`.
- A `LICENSE` file: all rights reserved.

### Changed

- The app now needs .NET 10, and is organised in the Pivot.Framework structure, the first step towards the
  Android app ([ADR-0003](docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md)): `src/Core`
  (Domain, Application), `src/Infrastructure` and `src/Containers`, with feature folders, and a `test/` tree with
  one xUnit project per source project plus end-to-end tests.
- The web app moved to `src/Containers/UI/Hosting/KbcTracker.Web`, in the `KbcTracker.slnx` solution, and is now a
  client of the backend: it stores nothing and holds no bank key, and its settings are only `Urls`,
  `AllowedHosts` and `Backend:BaseUrl`/`Backend:OwnerLogin`. Start the backend first; when it is unreachable,
  the pages say so. The bank's redirect URL stays `https://localhost:5001/callback`. See
  [Run the backend](docs/how-to/run-the-backend.md) to set up the database and import your data.
- The domain is built on Pivot.Framework's entities, aggregates and `Result`/`Error` types (ADR-0003 step 2). The
  data files keep their format, and records older versions could save with a blank id still load. A category name
  without letters or digits, a blank payee name, a change to a fixed payment that no longer exists and a request
  missing a field are now refused with a message instead of being saved or failing.
- A transaction the bank sends that cannot be read (for example with a malformed currency) is now left out, and
  every page says how many were left out, until a sync of the whole history finds none; the other transactions
  are still saved. A blank bank reference no longer becomes an id, and
  a transaction an older version stored with a blank id keeps its place on the next sync instead of being added
  twice.
- The build now runs the shared analyzers with warnings as errors, and restores packages from nuget.org and, for
  Pivot.Framework packages only, from its private feed ([ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md)).

### Removed

- The web app's JSON data folder, its `--sync` option (it now exits with code 2 and points to the backend's
  `--sync`), its own automatic sync, and its `DataDirectory`, `AutoSyncHours`, `EnableBanking` and `Categories`
  settings: the backend took them over.
- `scripts/install-daily-sync.ps1`. The backend syncs while it runs; remove the old scheduled task with
  `Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false`
  (see [Keep syncing every day](docs/how-to/sync-every-day.md)).

### Fixed

- When a sync, a bank connection or the bank's callback fails, the web app now returns you to the page you came
  from and shows the backend's message there once, instead of a raw error document. A failed change on a tab now
  shows the backend's message instead of its raw answer.
- When the backend refuses the web app (`403`) or is not ready (`409`, for example before `--import-json` or
  `--initialize`), the web app now says so and tells you what to check, instead of showing "Backend unavailable".
- A sync that was running while you reconnected no longer overwrites the new connection, and a sync error caused
  by a file (for example a missing private key) is shown on the pages like any other.
- Automatic categories: a keyword now matches only at the start of a word. "AD " (AD Delhaize) no longer puts
  shops such as Azad or Rad Huy in Groceries. A keyword glued to the end of a longer word (for example
  "APOTHEEK" in "STADSAPOTHEEK") no longer matches either; add the longer word as a keyword if you need it.
- Removing a fixed payment whose shop names contain accents no longer brings its suggestion back.
