# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Documentation in `docs/`: a get-started tutorial, how-to guides, reference pages for the configuration, tabs and
  data folder, and explanations of the classification and the architecture. Each project has a `README.md`.
- A `LICENSE` file: all rights reserved.

### Changed

- The app now needs .NET 10, and is organised in the Pivot.Framework structure, the first step towards the
  Android app ([ADR-0003](docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md)): `src/Core`
  (Domain, Application), `src/Infrastructure` and `src/Containers`, with feature folders, and a `test/` tree with
  one xUnit project per source project plus end-to-end tests.
- The web app moved to `src/Containers/UI/Hosting/KbcTracker.Web`, in the `KbcTracker.slnx` solution. Start it
  with `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`; its settings are now in that folder's
  `appsettings.json`. Re-run `scripts/install-daily-sync.ps1` so the daily sync runs the new `KbcTracker.Web.dll`.
- The domain is built on Pivot.Framework's entities, aggregates and `Result`/`Error` types (ADR-0003 step 2). The
  data files keep their format, and records older versions could save with a blank id still load. A category name
  without letters or digits, a blank payee name, a change to a fixed payment that no longer exists and a request
  missing a field are now refused with a message instead of being saved or failing.
- A transaction the bank sends that cannot be read (for example with a malformed currency) is now left out, and
  every page says how many were left out, until a sync of the whole history finds none; the other transactions
  are still saved, and the daily `--sync` run prints the count. A blank bank reference no longer becomes an id, and
  a transaction an older version stored with a blank id keeps its place on the next sync instead of being added
  twice.
- The build now runs the shared analyzers with warnings as errors, and restores packages from nuget.org and, for
  Pivot.Framework packages only, from its private feed ([ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md)).

### Fixed

- Automatic categories: a keyword now matches only at the start of a word. "AD " (AD Delhaize) no longer puts
  shops such as Azad or Rad Huy in Groceries. A keyword glued to the end of a longer word (for example
  "APOTHEEK" in "STADSAPOTHEEK") no longer matches either; add the longer word as a keyword if you need it.
- Removing a fixed payment whose shop names contain accents no longer brings its suggestion back.
