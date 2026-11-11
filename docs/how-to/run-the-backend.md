# Run the backend

The backend holds your history and decisions in PostgreSQL, owns the Enable Banking key and the sync, and serves
the API the web app uses ([ADR-0004](../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md)).
You need the .NET 10 SDK and the private-feed credentials described in the root README.

There are two ways to run it. The Aspire AppHost starts everything at once and is the recommended one; the manual
way below runs each program yourself against a PostgreSQL server you provide.

## Run everything with Aspire

The AppHost starts PostgreSQL in Docker, applies the migrations, then starts the backend and the web app
([ADR-0005](../adr/0005-run-the-app-on-the-pc-with-aspire.md)). You also need Docker running.

1. Store the Enable Banking settings in the backend's user secrets, once. The AppHost provides the database, so
   skip the connection string:

   ```powershell
   $project = 'src/Containers/API/KbcTracker.ApiService'
   dotnet user-secrets set "EnableBanking:ApplicationId" "<application-id>" --project $project
   dotnet user-secrets set "EnableBanking:PrivateKeyPath" "<full-path-of-the-pem-file>" --project $project
   ```

1. Trust the local HTTPS certificate, once, then start the AppHost:

   ```powershell
   dotnet dev-certs https --trust
   dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost
   ```

   In Visual Studio, set `KbcTracker.AppHost` as the startup project with its `https` profile and press F5.

1. The Aspire dashboard opens at <https://localhost:17180>. Wait until `migrate` shows **Finished** and `api` and
   `web` show **Running**. The first run downloads the PostgreSQL image. If `api` stays **Waiting**, open the
   console log of `migrate`: a failed migration keeps the backend from starting until it is fixed.
1. On the first run, `seed` gives the new database the default categories and empty decisions; on later runs it
   leaves your data alone.
1. Open <https://localhost:5001>.

PostgreSQL keeps running in its container after you stop the AppHost, and your data stays in the Docker volume
`kbctracker-postgres`. Its password is generated on the first run and kept in the AppHost's user secrets as
`Parameters:postgres-password`. Do not remove either, or the history is lost or locked out. Migrations run on every
start without a backup first, so back up before you pull a change that adds a migration.

A database you set up by hand earlier is not used by the AppHost. The connection string in the backend's user
secrets only applies when you start the backend on its own.

## Run it yourself

Set it up in this order: configure, migrate, import or initialise, start the backend, start the web app. You need
a PostgreSQL database.

### Configure

Use a database of its own. Store the connection string and the Enable Banking settings as user secrets, which
the backend reads in Development:

```powershell
$project = 'src/Containers/API/KbcTracker.ApiService'
dotnet user-secrets set "ConnectionStrings:KbcTracker" "Host=localhost;Database=kbctracker;Username=kbctracker;Password=<local-password>" --project $project
dotnet user-secrets set "EnableBanking:ApplicationId" "<application-id>" --project $project
dotnet user-secrets set "EnableBanking:PrivateKeyPath" "<full-path-of-the-pem-file>" --project $project
```

Replace the placeholders locally. Never commit the connection string or the private key. The other settings are in
[Configuration](../reference/configuration.md).

### Prepare the database

Back up any database that holds history before you apply migrations; normal startup never migrates.

```powershell
dotnet run --project src/Containers/API/KbcTracker.ApiService -- --migrate
```

Then fill the empty database once, with one of these:

- **You used an older version.** Import its JSON data folder, which keeps your history, categories, decisions and
  bank connection. The folder is only read; keep it as an archive.

  ```powershell
  dotnet run --project src/Containers/API/KbcTracker.ApiService -- --import-json "$env:LOCALAPPDATA\KbcTracker"
  ```

- **You start fresh.** Create the default categories and empty decisions.

  ```powershell
  dotnet run --project src/Containers/API/KbcTracker.ApiService -- --initialize
  ```

The import refuses a database that already holds data; `--initialize` leaves such a database alone. The default
categories it creates are defined in the code; change them afterwards on the **Categories** tab. The AppHost seeds
its database on its first start, so an import into it only works before that first start or after removing the
volume `kbctracker-postgres`. Until one of them has run, the backend refuses decisions and new bank connections with `409 Conflict`, because anything saved first would make the import impossible. The web app
shows the backend's reason, on a page of its own or as a notice on the page you were on. See
[Command line](../reference/command-line.md) for the exit codes.

### Start the backend and the web app

```powershell
dotnet run --project src/Containers/API/KbcTracker.ApiService
```

The backend listens on <http://127.0.0.1:5080>; `BackendPort` changes the port, and the listener stays on IPv4
loopback. In Development, <http://127.0.0.1:5080/swagger> shows the API. The committed
[OpenAPI contract](../api/openapi.json) describes every endpoint.

Then start the web app in a second terminal:

```powershell
dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
```

The web app reaches the backend at `Backend:BaseUrl`. When the backend is down, its pages say so; they never fall
back to local data.

## Understand the access boundary

Development allows local requests without a login. Outside Development, both programs accept a request only from
a loopback peer that carries a `Tailscale-User-Login` header equal to the configured owner
(`BackendAccess:OwnerLogin` for the backend, `Backend:OwnerLogin` for the web app). A blank owner denies access.
Browser writes from another origin are refused. `/health` reports only that the process runs; it does not check
the database or the bank. Swagger is available only in Development.

The identity header is trusted only because Tailscale Serve must be the only remote route to the loopback
listeners; a local process can supply it too. Never expose a listener directly or use Funnel. Add the tailnet
host name to `AllowedHosts` before you serve the apps through Tailscale.

## What is not built yet

- Docker Compose, Tailscale Serve and automated backups. Until then, run the app with the AppHost or yourself on
  your PC, and back up with `pg_dump`.
- Not yet verified: that Docker publishes the backend on loopback only, which Host header Tailscale Serve passes,
  and that Enable Banking accepts a `*.ts.net` redirect URL.
- The Android app and its SQLite cache.

## Refresh the contract

Install the matching Swagger tool outside the repository, then generate the document from the built API:

```powershell
dotnet tool install Swashbuckle.AspNetCore.Cli --version 10.3.1 --tool-path "$env:TEMP/kbc-swagger-tools"
dotnet build src/Containers/API/KbcTracker.ApiService
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__KbcTracker = 'Host=localhost;Database=kbctracker_design;Username=kbctracker'
& "$env:TEMP/kbc-swagger-tools/swagger.exe" tofile --output docs/api/openapi.json src/Containers/API/KbcTracker.ApiService/bin/Debug/net10.0/KbcTracker.ApiService.dll v1
```

Generation does not connect to this design-time database. The API tests compare the runtime document with the
committed contract. See [Run the tests](run-the-tests.md) for the Docker prerequisite.
