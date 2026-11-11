# ADR-0005: Run the app on the PC with an Aspire AppHost

- Status: Proposed
- Date: 2026-11-11
- Deciders: Gihed Annabi
- Amends: [ADR-0004](0004-serve-both-apps-from-one-postgresql-backend-and-api.md) (hosting on the PC; migrations)

## Context

ADR-0004 split KbcTracker into a PostgreSQL backend and API and a web app that is only a client of it. Running the
app now takes a PostgreSQL server you set up yourself, a connection string in user secrets, and two programs started
in the right order. Starting only the web app, which Visual Studio does with its default startup project, gives
pages that say the backend is unavailable.

ADR-0004 left out the Aspire AppHost and ServiceDefaults of Pivot's canonical structure, to revisit "when
development needs more than the database and the API". The user now wants one start for the whole app.

Forces:

- KBC returns only about 90 days, so the history in PostgreSQL must survive every restart and must never be lost.
- Docker already runs on the PC; the PostgreSQL tests use the pinned image `postgres:17.6-alpine`.
- The API binds its own IPv4 loopback listener (`BackendPort`) and the web app fixes `https://localhost:5001`,
  which the bank's redirect URL uses. Both decide access from the caller's address.
- The Enable Banking key and application id stay in the API's user secrets.

Options considered:

1. Keep starting PostgreSQL, the API and the web app by hand, as documented today. Rejected: three steps in the
   right order is what leads to running the web app alone.
1. An Aspire AppHost that starts all three, with PostgreSQL in a container. Chosen.
1. An Aspire AppHost that starts the API and the web app against a PostgreSQL server you run yourself. Rejected by
   the user: it still needs a server set up and started by hand.
1. Docker Compose for everything, as ADR-0004 plans for the hosted setup. Rejected for now: it needs a Dockerfile
   per host and gives no dashboard or debugging from Visual Studio; it stays the next step for the hosted setup.

## Decision

We add an Aspire 13.6.5 AppHost, `src/Containers/Aspire/Hosting/KbcTracker.AppHost`, as the way to run the app on
the PC, and `src/Containers/Aspire/Defaults/KbcTracker.ServiceDefaults`, which both hosts use (option 2).

- **PostgreSQL** runs from `postgres:17.6-alpine` in a container with a persistent lifetime and the named volume
  `kbctracker-postgres`. The database is `kbctracker`, offered to the API as `ConnectionStrings:KbcTracker`. The
  password is a generated AppHost parameter kept in the AppHost's user secrets (`Parameters:postgres-password`),
  because the volume is initialised with it. Aspire publishes the port on `127.0.0.1` only.
- **Migrations run on every start.** A one-shot resource runs the API with `--migrate`; the API starts only after
  it completed successfully. This replaces, for this setup, ADR-0004's rule that migrations are an explicit step
  after a fresh backup. There are no backups yet, so a schema change applies without one.
- **The one-time fill** is two resources started from the dashboard, never on their own: `import-json` (the folder
  in `KbcTracker:JsonArchive`, by default `%LOCALAPPDATA%\KbcTracker`) and `initialize`. Both refuse a database
  that already holds data.
- **Addresses do not change.** The API keeps `http://127.0.0.1:5080` and the web app `https://localhost:5001`, with
  no Aspire proxy in front of either; the web app calls the API on `127.0.0.1`. Forwarded headers stay off for both,
  so no local caller can rewrite the address the access checks rely on. Both run in Development: outside it they
  require the Tailscale owner header, which nothing sends on the PC.
- **ServiceDefaults** adds OpenTelemetry logs, traces and metrics for ASP.NET Core, outgoing HTTP calls and the
  runtime, exported only when the AppHost gives an OTLP endpoint (the dashboard). The HTTP instrumentations keep
  their default redaction of query-string values, so the bank's authorization code never reaches a trace. Database
  commands are not traced, and the API logs SQL text and outgoing HTTP calls only at `Warning`, so neither reaches
  the exported logs. It adds no health endpoints and no HTTP resilience handler: the API already serves
  `/health`, and retries would replay the bank's one-time code and cut the five-minute sync timeout.
- **The AppHost** uses the dashboard and orchestrator NuGet packages (`AspireUseCliBundle=false`), so `dotnet run`
  and Visual Studio start it without the Aspire CLI. The build warning ASPIRE010, which only reports that choice, is
  suppressed in the AppHost project under a dated waiver in
  [ADR-0002](0002-adopt-the-shared-analyzer-set.md).

The hosts still run on their own, with their own settings, when started without the AppHost.

## Consequences

- One start runs the whole app, from Visual Studio (the AppHost as startup project) or `dotnet run`, and the
  dashboard shows each resource's state, logs and traces.
- The history moves into the Aspire container's volume. A database set up by hand before is not used by the
  AppHost; its connection string in the API's user secrets only applies when the API runs on its own. Keep one of
  the two as the database you use.
- Docker must run whenever the app runs. Removing the volume, or losing the AppHost's user secrets, loses or locks
  the history; backups (ADR-0004) are still to be built and are more urgent now.
- Migrations apply without a backup step; a faulty migration stops the API before it serves anything, but cannot be
  rolled back from the AppHost.
- Docker Compose, Tailscale Serve and backups (ADR-0004) remain the next hosting step; Aspire can generate Compose
  files later if that helps.
- The AppHost model has its own tests (`test/Containers/Aspire/Hosting/KbcTracker.AppHost.Tests`) that start nothing.
- ServiceDefaults has no test project: it only registers OpenTelemetry, which the AppHost run shows working.

## Revisit when

- Backups exist: run them before the migration resource.
- The app moves to Docker Compose and Tailscale Serve, or a second machine runs it.
