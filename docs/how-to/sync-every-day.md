# Keep syncing every day

KBC only returns about the last 90 days. You keep your full history by syncing at least every few weeks; daily is
safest.

## Let the backend sync

While the backend runs, it syncs by itself. Every 30 minutes it checks the bank connection, and it syncs when the
consent is still valid and the last sync is older than `AutoSyncHours` (6 hours by default). The web app does not
need to be open. See [Configuration](../reference/configuration.md) to change the interval or turn it off with
`0`.

Keep the backend running, or start it at least once a day. Running it as a Docker Compose service that starts with
the PC is planned ([ADR-0004](../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md)).

## Sync once from the command line

To sync without starting the API, for example from your own scheduled task, run:

```powershell
dotnet run --project src/Containers/API/KbcTracker.ApiService -- --sync
```

It prints the number of new transactions and exits with code 0, or prints that the sync failed and exits with
code 1. The backend uses a database lock, so this command and a running backend never sync at the same time.

## After an update

The scheduled task that earlier versions installed with `scripts/install-daily-sync.ps1` ran the web app's
`--sync`, which no longer syncs. Remove it:

```powershell
Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false
```
