# KbcTracker

A personal expense tracker for a KBC Brussels current account: it reads your transactions read-only through
Enable Banking, finds your fixed payments and shows your spending per month. Built with .NET 10 and ASP.NET Core.

## Status

**Beta.** Owned by Gihed Annabi ([GitHub issues](https://github.com/AnnabiGihed/Bank-Tracker/issues)).
KbcTracker runs as one PostgreSQL backend and API on Pivot.Framework, used by the local web app and, later, an
Android app ([ADR-0004][adr-0004], proposed). The backend owns the history, your decisions, the bank key and the
sync; the web app is a client of its API and stores nothing. Docker Compose, Tailscale Serve and backups are not
built yet, so today you start the backend and the web app yourself on your PC.

## Run locally

Prerequisites: the .NET 10 SDK, a PostgreSQL database, the Pivot.Framework feed variables below, and an Enable
Banking application with its private key (see [Get started](docs/tutorials/get-started.md)).

1. Configure the backend and prepare its database once: apply the migrations, then either import your existing
   JSON data folder or start fresh. See [Run the backend](docs/how-to/run-the-backend.md).
1. Start the backend, then the web app, in two terminals:

   ```powershell
   dotnet run --project src/Containers/API/KbcTracker.ApiService
   ```

   ```powershell
   dotnet dev-certs https --trust
   dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
   ```

Open <https://localhost:5001>. Run the tests with `dotnet test`; the database, API and end-to-end tests need
Docker (see [Run the tests](docs/how-to/run-the-tests.md)).

## Documentation

- **Start here**: [docs/index.md](docs/index.md)
- **First-time setup**: [docs/tutorials/get-started.md](docs/tutorials/get-started.md)
- **How-to guides**: [docs/how-to/](docs/how-to/)
- **Reference** (configuration, command line, tabs, data): [docs/reference/](docs/reference/)
- **How it works** (classification, architecture): [docs/explanation/](docs/explanation/)
- **API contract**: [docs/api/openapi.json](docs/api/openapi.json)
- **Decisions**: [docs/adr/](docs/adr/README.md)
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)
- **Issues**: <https://github.com/AnnabiGihed/Bank-Tracker/issues>

## Repository layout

The solution follows the Pivot.Framework structure ([ADR-0004][adr-0004]), with `Features/<Feature>/` folders
inside every project.

| Path | What it holds |
| --- | --- |
| `src/Core/KbcTracker.Domain` | Transactions, categories, fixed payments, the bank connection, and the `Analysis` service. |
| `src/Core/KbcTracker.Application` | Use cases as mediator commands and queries (sync, consent, workspace, decisions) and their ports. |
| `src/Infrastructure/KbcTracker.Infrastructure` | The Enable Banking client and its mapping, and the reader of the old JSON data folder. |
| `src/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore` | PostgreSQL persistence: the EF Core context, migrations, repositories, unit of work and the one-time import. |
| `src/Containers/API/KbcTracker.ApiService` | The backend: the API, the automatic sync and the maintenance commands. |
| `src/Containers/UI/Core/KbcTracker.Shared` | The typed API client the user interfaces share. |
| `src/Containers/UI/Hosting/KbcTracker.Web` | The local web app: server-rendered pages over the API. |
| `test/` | One xUnit test project per tested source project, in the same folders, plus `KbcTracker.E2E.Tests`. |
| `docs` | Tutorials, how-to guides, reference, explanations, the API contract and decisions. |

Build settings, analyzers and package versions are shared in `Directory.Build.props` and `Directory.Packages.props`.
Pivot.Framework packages come from a private feed: set `PIVOT_PACKAGES_USER` and `PIVOT_PACKAGES_TOKEN`
(a GitHub token with `read:packages`) before you restore a project that uses them.

## Tooling deviations

The deviations from the static-analysis rules, with their expiry or review dates, are listed in
[ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md). In short:

- The web app (`src/Containers/UI/Hosting/KbcTracker.Web`) is not held to warnings as errors until its pages are
  brought up to the analyzers, at the latest by 2027-06-30.
- StyleCop SA1309, SA1514 and SA1642, and CA1716, are off because they contradict the house C# style.
- Parts of the Pivot.Framework structure are left out for a single-user, home-hosted backend (ADR-0004).
- There is no CI, SonarCloud, Markdownlint or published docs site yet.

## License

All rights reserved. See [LICENSE](LICENSE).

[adr-0004]: docs/adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md
