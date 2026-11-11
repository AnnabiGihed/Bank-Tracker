# KbcTracker.AppHost

The Aspire AppHost: one start runs the whole app on your PC ([ADR-0005](../../../../../docs/adr/0005-run-the-app-on-the-pc-with-aspire.md)).
It starts PostgreSQL in Docker, applies the migrations, then starts the API and the web app, and opens the Aspire
dashboard.

| Path | Responsibility |
| --- | --- |
| `Program.cs` | The resources and their start order: `postgres` and its `KbcTracker` database, `migrate`, `seed` (fills an empty database), `api` and `web`. |
| `Properties/launchSettings.json` | The `https` and `http` profiles with the fixed dashboard addresses. |

Start it with `dotnet run --project src/Containers/Aspire/Hosting/KbcTracker.AppHost`, or make it the startup
project in Visual Studio with the `https` profile. The dashboard is at <https://localhost:17180>; the first run
downloads the PostgreSQL image. See [Run the backend](../../../../../docs/how-to/run-the-backend.md).
Tests: `test/Containers/Aspire/Hosting/KbcTracker.AppHost.Tests`.
