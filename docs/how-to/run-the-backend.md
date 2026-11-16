# Run the backend

The backend holds your history and decisions in PostgreSQL, owns the Enable Banking key and the sync, and serves
the API the web app and the Android app use ([ADR-0004](../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md)).
You need the .NET 10 SDK and the private-feed credentials described in the root README.

There are two ways to run it. The Aspire AppHost starts everything at once and is the recommended one; the manual
way below runs each program yourself against a PostgreSQL server you provide.

## Run everything with Aspire

The AppHost starts PostgreSQL in Docker, applies the migrations, then starts the backend and the web app
([ADR-0005](../adr/0005-run-the-app-on-the-pc-with-aspire.md)). You also need Docker running.

1. Set up sign-in once ([Set up sign-in](set-up-sign-in.md)): the Google client and the owner list, stored in the
   backend's user secrets. The AppHost provides the web app's client secret and the database.
1. Store the Enable Banking settings in the backend's user secrets, once. Skip the connection string:

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
1. Open <https://localhost:5001> and sign in with the owner's account.

PostgreSQL keeps running in its container after you stop the AppHost, and your data stays in the Docker volume
`kbctracker-postgres`. Its password is generated on the first run and kept in the AppHost's user secrets as
`Parameters:postgres-password`. Do not remove either, or the history is lost or locked out. Migrations run on every
start without a backup first, so back up before you pull a change that adds a migration.

A database you set up by hand earlier is not used by the AppHost. The connection string in the backend's user
secrets only applies when you start the backend on its own.

## Run it yourself

Set it up in this order: configure (sign-in included), migrate, import or initialise, start the backend, start the
web app. You need a PostgreSQL database.

### Configure

Use a database of its own. Store the connection string and the Enable Banking settings as user secrets, which
the backend reads in Development:

```powershell
$project = 'src/Containers/API/KbcTracker.ApiService'
dotnet user-secrets set "ConnectionStrings:KbcTracker" "Host=localhost;Database=kbctracker;Username=kbctracker;Password=<local-password>" --project $project
dotnet user-secrets set "EnableBanking:ApplicationId" "<application-id>" --project $project
dotnet user-secrets set "EnableBanking:PrivateKeyPath" "<full-path-of-the-pem-file>" --project $project
```

Then set up sign-in as in [Set up sign-in](set-up-sign-in.md): the Google client, the owner list, and the secret
both programs share:

```powershell
dotnet user-secrets set "Authentication:Clients:Web:Secret" "<long-random-value>" --project $project
dotnet user-secrets set "Authentication:Authority" "http://localhost:5080/" --project src/Containers/UI/Hosting/KbcTracker.Web
dotnet user-secrets set "Authentication:Clients:Web:Secret" "<same-value>" --project src/Containers/UI/Hosting/KbcTracker.Web
```

Replace the placeholders locally. Never commit the connection string, the private key or a secret. The other settings
are in [Configuration](../reference/configuration.md).

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

Without sign-in settings the backend starts but nobody can use it, and the web app refuses to start without its client
secret. Then start the web app in a second terminal:

```powershell
dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
```

The web app is a Blazor app: it keeps a live connection to its host while a tab is open. It reaches the backend at
`Backend:BaseUrl`. Open <https://localhost:5001>; the sign-in page sends you to Google. When the backend is down, its
pages say so; they never fall back to local data. To use the Android app against this backend, see [Run the Android app](run-the-android-app.md).

## Understand the access boundary

Both programs let in only the owner ([Sign-in](../explanation/sign-in.md)). Every API endpoint except `/health` and the
sign-in endpoints needs a token of the owner, or of the web app's own client. The web app sends everything else to its
sign-in page: pages, the live connection, `/callback` and `/export.csv`. Browser writes from another origin are
refused. `/health` reports only that the process runs; it does not check the database or the bank. Swagger is
available only in Development. The listeners stay on loopback; a server needs a TLS front end first.

## What is not built yet

- Docker Compose and automated backups. Until then, run the app with the AppHost or yourself on
  your PC, and back up with `pg_dump`.
- Hosting on a server: its domain and TLS, the bank key on the server and the bank's redirect URL.
- The Android app's production backend (it only works against your PC in the emulator) and its SQLite cache.

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
