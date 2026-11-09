# KbcTracker

A personal expense tracker for a KBC Brussels current account: it reads your transactions read-only through
Enable Banking, finds your fixed payments and shows your spending per month. Built with .NET 10 and ASP.NET Core.

## Status

**Beta.** Owned by Gihed Annabi ([GitHub issues](https://github.com/AnnabiGihed/Bank-Tracker/issues)).
The local web app works today; it is moving to an Android app built on Pivot.Framework ([ADR-0003][adr-0003]).

## Run locally

Prerequisites: the .NET 10 SDK, and an Enable Banking application with its private key
(see [Get started](docs/tutorials/get-started.md)).

```powershell
dotnet dev-certs https --trust
dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
```

Open <https://localhost:5001>. Run the tests with `dotnet test`.

## Documentation

- **Start here**: [docs/index.md](docs/index.md)
- **First-time setup**: [docs/tutorials/get-started.md](docs/tutorials/get-started.md)
- **How-to guides**: [docs/how-to/](docs/how-to/)
- **Reference** (configuration, tabs, data folder): [docs/reference/](docs/reference/)
- **How it works** (classification, architecture): [docs/explanation/](docs/explanation/)
- **Decisions**: [docs/adr/](docs/adr/README.md)
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)
- **Issues**: <https://github.com/AnnabiGihed/Bank-Tracker/issues>

## Repository layout

The solution follows the Pivot.Framework structure ([ADR-0003][adr-0003]), with `Features/<Feature>/` folders
inside every project.

| Path | What it holds |
| --- | --- |
| `src/Core/KbcTracker.Domain` | Transactions, categories, fixed payments, the bank connection, and the `Analysis` service. |
| `src/Core/KbcTracker.Application` | Use cases (the bank sync) and the ports to storage and to the bank. |
| `src/Infrastructure/KbcTracker.Infrastructure` | The Enable Banking client and its JSON mapping. |
| `src/Containers/UI/Hosting/KbcTracker.Web` | The local web app: pages, `POST /api`, JSON storage, auto-sync. |
| `test/` | One xUnit test project per source project, in the same folders, plus `KbcTracker.E2E.Tests`. |
| `scripts` | `install-daily-sync.ps1`: a Windows scheduled task that syncs every day. |
| `docs` | Tutorials, how-to guides, reference, explanations and decisions. |

Build settings, analyzers and package versions are shared in `Directory.Build.props` and `Directory.Packages.props`.
Pivot.Framework packages come from a private feed: set `PIVOT_PACKAGES_USER` and `PIVOT_PACKAGES_TOKEN`
(a GitHub token with `read:packages`) before you restore a project that uses them.

## Tooling deviations

The deviations from the static-analysis rules, with their expiry or review dates, are listed in
[ADR-0002](docs/adr/0002-adopt-the-shared-analyzer-set.md). In short:

- The web app (`src/Containers/UI/Hosting/KbcTracker.Web`) is not held to warnings as errors until it is retired.
- StyleCop SA1309, SA1514 and SA1642, and CA1716, are off because they contradict the house C# style.
- Parts of the Pivot.Framework structure are left out because the app runs on the phone (ADR-0003).
- There is no CI, SonarCloud, Markdownlint or published docs site yet.

## License

All rights reserved. See [LICENSE](LICENSE).

[adr-0003]: docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md
