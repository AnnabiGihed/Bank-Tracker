# Run the backend

The backend holds your history and decisions in PostgreSQL, owns the Enable Banking key and the sync, and serves
the API the web app uses ([ADR-0004](../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md)).
You need the .NET 10 SDK, a PostgreSQL database, and the private-feed credentials described in the root README.

Set it up in this order: configure, migrate, import or initialise, start the backend, start the web app.

## Configure

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

## Prepare the database

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

Both refuse a database that already holds data. Until one of them has run, the backend refuses decisions and new
bank connections with `409 Conflict`, because anything saved first would make the import impossible. The web app
shows this as "Backend not ready", or as a notice on the page you were on. See
[Command line](../reference/command-line.md) for the exit codes.

## Start the backend and the web app

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

- Docker Compose, Tailscale Serve and automated backups. Until then, run both programs yourself and back up with
  `pg_dump`.
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
