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
- The build now runs the shared analyzers with warnings as errors, and restores packages from nuget.org and, for
  Pivot.Framework packages only, from its private feed ([ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md)).

### Fixed

- Automatic categories: a keyword now matches only at the start of a word. "AD " (AD Delhaize) no longer puts
  shops such as Azad or Rad Huy in Groceries. A keyword glued to the end of a longer word (for example
  "APOTHEEK" in "STADSAPOTHEEK") no longer matches either; add the longer word as a keyword if you need it.
- Removing a fixed payment whose shop names contain accents no longer brings its suggestion back.
