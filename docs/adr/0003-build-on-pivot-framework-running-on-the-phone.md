# ADR-0003: Build KbcTracker on Pivot.Framework, running entirely on the phone

- Status: Proposed
- Date: 2026-11-09
- Deciders: Gihed Annabi
- Supersedes: ADR-0001

## Context

ADR-0001 chose a .NET MAUI Android app with on-device sync and SQLite through `Microsoft.Data.Sqlite`, on a plain
.NET shared core. The user has since selected **Pivot.Framework** (`framework-selection`), whose scaffolding rule
makes the `src/Containers | Core | Infrastructure` layout, feature-first folders, mediator-based use cases and a
mirrored `test/` tree mandatory.

Pivot.Framework is built for server services: an Aspire AppHost, an API container, Keycloak authentication, and EF
Core persistence on SQL Server or PostgreSQL. Inspecting its 2.0.0 source showed:

- `Pivot.Framework.Domain` and `Pivot.Framework.Application` are plain .NET and can run inside a MAUI app.
- `Pivot.Framework.Infrastructure.Persistence.EntityFrameworkCore` references the SQL Server provider, but only its
  read/write-context helper calls `UseSqlServer`. SQL Server column types appear only in the optional audit,
  event-store and saga tables. Its base context and unit of work can therefore be given the EF Core SQLite
  provider, provided those optional features stay off. This is not yet proven on Android.
- Its MAUI package is a Keycloak login client for a Pivot API.

Forces:

- One user, one bank account, read-only. The data is personal and must not leave the user's control.
- The bank consent is given through itsme at KBC; that proves the user to the bank, not to the app.
- KBC returns only about 90 days, so the history must be kept on a store the user owns.
- The packages are published on the user's GitHub Packages feed, which needs a token with `read:packages`.
- Pivot's packages are AGPL-3.0; this repository is "all rights reserved"; the user owns both.

Options considered:

1. Pivot on the phone: the MAUI app is the only container; Pivot domain, application and EF Core persistence on
   SQLite run on the device.
1. A Pivot server on the home PC (API, SQL Server, Hangfire sync, Keycloak) with the phone app as a client over
   Tailscale.
1. A Pivot server on Azure (Container Apps, Azure SQL, Key Vault) with the phone app as a client.
1. Stay on plain .NET (ADR-0001) and drop Pivot.

## Decision

We build KbcTracker on Pivot.Framework 2.0.0 and run it entirely on the phone.

- **Layout.** We use Pivot's canonical structure: `src/Containers`, `src/Core`, `src/Infrastructure`, with
  feature-first folders (`Features/<Feature>/…`, `Features/Shared/`) in every project, and a `test/` tree that mirrors
  `src/` with one test project per source project plus `KbcTracker.E2E.Tests`.
- **Containers.** The Android app (`src/Containers/UI/Hosting/KbcTracker.Mobile`, .NET MAUI) is the target container.
  Until it exists, the local web host moves to `src/Containers/UI/Hosting/KbcTracker.Web` and keeps working.
- **Core.** `KbcTracker.Domain` holds the models and the `Analysis` domain service; `KbcTracker.Application` holds
  the use cases. Use cases move to Pivot's mediator-based commands and queries feature by feature.
- **Infrastructure.** `KbcTracker.Infrastructure` holds the Enable Banking client.
  `KbcTracker.Persistence.EntityFrameworkCore` holds a `PivotDbContextBase` context on the EF Core SQLite provider,
  with Pivot's unit of work and repositories, and imports the existing JSON files once. The audit, event-store and
  saga features stay off.
- **Messaging.** Domain events use Pivot's in-process publisher. There is no broker, outbox drain or integration
  event, because there is one process.
- **No authentication.** The data never leaves the phone; the device lock protects it. No Keycloak.
- **Tests.** xUnit with AwesomeAssertions and NSubstitute, the stack of Pivot's own tests.
- **Packages.** Pivot packages come from `https://nuget.pkg.github.com/AnnabiGihed/index.json` through package
  source mapping (`Pivot.Framework.*`). Credentials come from the `PIVOT_PACKAGES_USER` and `PIVOT_PACKAGES_TOKEN`
  environment variables; no token is ever committed.

These parts of Pivot's canonical structure are deliberately left out:

| Canonical element | Why it is left out | Revisit when |
| --- | --- | --- |
| Aspire AppHost and ServiceDefaults ("Aspire always") | There is no server process to orchestrate; Aspire does not host an Android app. | A server is added |
| `Containers/API` service | Nothing calls the domain over the network. | A second device or a web front end needs the data |
| Keycloak authentication | Single user; the data stays on the device. | Data leaves the phone |
| SQL Server / PostgreSQL | Not available on Android; EF Core SQLite is. | A server is added |
| `KbcTracker.IntegrationEvents` | One process: no integration events exist. | A second service exists |
| A `Dockerfile` per deployable host | Nothing is deployed as a container: the phone app ships as an Android package, and the web host only runs locally until it is retired. | A server is added |

These canonical elements are not met yet and are reached during the steps below:

| Canonical element | Interim state | Reached in |
| --- | --- | --- |
| Domain aggregates and repositories (`Features/<F>/Aggregates` as `AggregateRoot<TId>`, `Repositories/I…Repository`) | Plain classes and records in `Aggregates/` and `ValueObjects/`; `UserData` and `CategoryDefaults` placed provisionally | Step 2 (types), step 3 (repositories) |
| Persistence in `src/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore` | The JSON `Store` (`IStore`) lives in the web host, `Features/Shared/Storage` | Step 3 |
| Application slices `Commands/`, `Queries/`, `EventHandlers/` with the mediator | `Features/<F>/Services` (`SyncService`) and `Features/<F>/Abstractions` (`IStore`, `IBankTransactionSource`) | Step 4 (`IStore` is replaced by repositories in step 3) |
| UI host in the canonical form: Blazor with `Features/<Feature>/Pages/`, and `UI/Core/KbcTracker.ViewModels` | The legacy web host renders server-side HTML strings, one page class per feature folder | Step 5: the MAUI app (Blazor Hybrid) follows the UI slices; the web host is retired |

The delivery is split into steps, each on its own branch and keeping the app working and `RealDataTests` green:

1. Canonical layout, feature folders, xUnit test tree, tabs (structure only, no behaviour change).
1. Domain on Pivot types (`Entity`, `AggregateRoot`, `Result`/`Error`), keeping string transaction ids.
1. EF Core SQLite persistence with Pivot's unit of work, and a one-time import of the JSON files.
1. Use cases as mediator commands and queries; the web host sends them.
1. The MAUI Android app over the same Application layer; the web host is retired.
1. Several accounts, salary, quarterly and yearly payments, Loan and Salary views, notifications.

## Consequences

**Positive**

- One code structure and one set of conventions shared with the user's other Pivot services.
- No server to run, secure or keep reachable; no network exposure of bank data.
- The domain, application and persistence layers are the same whether the container is the web host or the app.

**Negative**

- Six canonical elements are left out (first table above); the structure is Pivot's, the runtime is not a Pivot service.
- Pivot's EF Core persistence on SQLite and on Android is unproven; step 3 may hit SQL Server-specific code.
- The SQL Server provider package ships inside the app although it is not used, which makes the app larger.
- Pivot's strongly typed ids are Guid-based; bank transaction ids are strings, so the transaction aggregate needs its
  own id type.
- Building needs a GitHub token with `read:packages`, and the packages have a single maintainer.
- Pivot is AGPL-3.0. Using it in this "all rights reserved" repository is only possible because the same person owns
  both; a second Pivot contributor would require a licence review.
- The 36 existing MSTest tests are rewritten for xUnit.

## Alternatives considered

- **Pivot server on the home PC** - rejected. The phone would depend on the PC being on and reachable, and Pivot's
  API path brings Keycloak, SQL Server and Hangfire for a single user.
- **Pivot server on Azure** - rejected. It costs money and puts the private key and the bank data in the cloud.
- **Plain .NET (ADR-0001)** - rejected. It would keep a one-off structure and hand-written domain, persistence and
  validation plumbing, where Pivot.Framework gives the same conventions, base types, unit of work and mediator
  pipeline as the user's other services, maintained in one place.
