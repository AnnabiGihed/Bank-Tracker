# ADR-0004: Serve the web app and the Android app from one PostgreSQL backend and API

- Status: Proposed
- Date: 2026-11-10
- Deciders: Gihed Annabi
- Supersedes: ADR-0003

## Context

ADR-0003 chose to run KbcTracker entirely on the phone: Pivot.Framework domain, application and EF Core persistence
on SQLite inside a .NET MAUI app, with no server and no login. Steps 1 (canonical layout) and 2 (domain on Pivot
types) of that plan are done and stay valid.

What changed: the user wants to keep **both** apps, the existing web app and the planned Android app, on the same
history and the same decisions (categories, fixed payments, dismissals). ADR-0003 named this as its revisit trigger
("a second device or a web front end needs the data"). An on-device store cannot share data between two apps, and
each app would need the Enable Banking private key to sync.

Forces:

- KBC returns only about 90 days, so the history is kept forever in a store the user owns and must never be lost.
- One user, one bank account, read-only access. The bank consent is given through itsme at KBC; that proves the
  user to the bank, not to the app.
- The home PC is the only machine that is always the user's; the phone reaches it through Tailscale. No cloud.
- The Enable Banking private key (`.pem`) must stay outside the repository and on as few machines as possible.
- Pivot.Framework 2.1.0 is on the user's feed with PostgreSQL persistence (`Infrastructure.Persistence.PostgreSQL`
  on EF Core 10 and Npgsql), the API helpers (`Containers.API`) and its own mediator. Docker is available on the PC.

Options considered:

1. Keep ADR-0003: each app on its own device store.
1. One backend on the home PC with PostgreSQL and an API; both apps are clients.
1. One backend on the home PC with SQLite.
1. One backend in Azure.

## Decision

We run one backend on the home PC: an ASP.NET Core API on Pivot.Framework 2.1.0 with one PostgreSQL database. The
web app and the Android app are both clients of that API. The Android app gets a SQLite cache later; it is not part
of this decision.

- **Persistence.** `src/Infrastructure/KbcTracker.Persistence.EntityFrameworkCore` holds a `PivotDbContextBase`
  context on Npgsql (`AddPostgreSqlContext`), Pivot's unit of work and repositories, and the EF Core migrations.
  Strongly typed ids map to `text`, `DateTimeOffset` values are stored in UTC (`timestamptz`), the bank's original
  JSON stays `text` so it round-trips exactly, and the user's decisions keep their owned fixed payments. The
  existing JSON data folder is imported once by an idempotent importer and then kept as a read-only archive.
- **API.** `src/Containers/API/KbcTracker.ApiService` on `Pivot.Framework.Containers.API`, with use cases as
  mediator commands and queries. The OpenAPI document and its UI exist in Development only.
- **The backend owns the bank.** Only the API host talks to Enable Banking, holds the private key and runs the sync:
  the connect flow (`/banks`, `/connect`, `/callback`) and the daily sync move there as a hosted service. Overlapping
  syncs are prevented with a PostgreSQL advisory lock. The Windows scheduled task and the `--sync` mode that write
  data directly are retired, so there is one writer.
- **Clients.** The web host becomes a client of the API through a typed client shared with the Android app
  (`src/Containers/UI/Core/KbcTracker.Shared`); it loses the database, the key and the sync. The API does not render
  the pages.
- **Hosting.** PostgreSQL and the API run under Docker Compose on the home PC, from pinned images, with a Dockerfile
  for the API, `restart: unless-stopped` and a database health check. The data lives in a named volume. Database
  migrations run as an explicit deployment step after a fresh backup, never silently at startup.
- **Backups.** A daily `pg_dump` is written outside Docker and copied off the PC, encrypted; a restore is tested
  regularly. They replace the daily JSON backups of the current store.
- **Network protection, no login.** The API publishes only on `127.0.0.1`; Tailscale Serve exposes it on the
  tailnet with HTTPS (a `*.ts.net` certificate), never Tailscale Funnel. A tailnet access rule limits it to the
  user's own devices. A middleware rejects requests whose `Tailscale-User-Login` header is not the owner's login;
  this is sound only because the API is reachable through Tailscale Serve alone. `AllowedHosts` is limited to the
  tailnet name and localhost, there is no CORS policy, and secrets, tokens, IBANs and the bank's raw JSON are never
  logged. PostgreSQL is not published beyond `127.0.0.1`.
- **Secrets.** The private key, the Enable Banking application id and the connection string come from user secrets
  in development and from Compose secrets (files outside the repository) on the PC. They are never committed or put
  in an image.
- **Tests.** xUnit with AwesomeAssertions and NSubstitute. Persistence, API and end-to-end tests run against a real
  PostgreSQL through Testcontainers, on the same pinned image as Compose; no in-memory provider.

These parts of Pivot's canonical structure are still left out:

| Canonical element | Why it is left out | Revisit when |
| --- | --- | --- |
| Aspire AppHost and ServiceDefaults | Docker Compose runs the PC; Testcontainers covers the tests. | Development needs more than the database and the API |
| Keycloak authentication | One user; the API is reachable only on the user's tailnet and checks the Tailscale identity. | A second user, or exposure beyond the tailnet |
| `KbcTracker.IntegrationEvents`, outbox and broker | One service and no domain events yet. | A second service, or the first domain event that must leave the process |
| Hangfire | One daily job; Pivot's Hangfire helper uses SQL Server storage and its dashboard needs a login. | Several scheduled jobs |
| Audit fields and soft delete | Bank history and the user's choices are never deleted (`LightweightAggregateRoot`, from ADR-0003). | n/a |
| Canonical UI (Blazor, `UI/Core/KbcTracker.ViewModels`) | The web host keeps its server-rendered pages until the Android app exists. | The Android app step |

The delivery continues in steps, each on its own branch and keeping the app working and `RealDataTests` green:

1. Done: canonical layout, feature folders, xUnit tests.
1. Done: domain on Pivot types.
1. PostgreSQL persistence: the context, mappings, migration and importer, tested against PostgreSQL; then
   repositories, `SyncService` on repositories and the unit of work, and the switch of the data to PostgreSQL.
1. Use cases as mediator commands and queries.
1. The API service: the sync, Enable Banking and the key move there; Docker Compose, Tailscale Serve and backups;
   the web host becomes a client of the API.
1. The Android app as a client of the API.
1. A SQLite cache in the Android app.
1. Several accounts, salary, quarterly and yearly payments, Loan and Salary views, notifications.

## Consequences

### Positive

- One history and one set of decisions for both apps.
- The private key and the bank access stay on one machine; the apps never see them.
- The backend is a regular Pivot service: EF Core persistence, API helpers, mediator and real-database tests.

### Negative

- The apps need the home PC to be on and reachable. Docker Desktop only runs after the user signs in, so nothing
  runs after an unattended reboot until then. The Android app is unusable offline until its cache exists.
- A database to back up and to restore-test; losing the Docker volume without a recent off-PC dump loses history
  the bank no longer has.
- The Enable Banking redirect URL becomes the API's tailnet address; that Enable Banking accepts a `*.ts.net`
  redirect is not yet verified.
- The tailnet and machine names appear in public certificate-transparency logs because of the HTTPS certificate.
- Unused packages come along: Pivot's EF Core package brings the SQL Server provider, `Containers.API` brings its
  Keycloak and Redis authentication packages, and the messaging package brings RabbitMQ and Quartz.
- Every protection relies on Tailscale and the localhost binding; a port published on all interfaces by mistake
  exposes the bank data to the home network.
- `RealDataTests` read the JSON data folder and need a database export or fixture once the data moves.
- Pivot is AGPL-3.0; the note from ADR-0003 still applies.

## Alternatives considered

- **Each app on its own device store (ADR-0003)** - rejected. Two apps could not share the history or the user's
  decisions, and each would need the private key to sync.
- **SQLite on the backend** - rejected. It would be lighter to run for one user, but Pivot's SQLite persistence is
  not on the feed at 2.1.0, and PostgreSQL is the provider Pivot ships and tests for servers.
- **Backend in Azure** - rejected, as in ADR-0003: it costs money and puts the key and the bank data in the cloud.
- **The API host also renders the web pages** - rejected. The pages would bypass the API the phone uses, and there
  would be two writers.
- **Keycloak on the API** - rejected for now; see the table above.
