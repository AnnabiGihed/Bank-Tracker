# Sync every day

KBC only returns about the last 90 days. You keep your full history by syncing regularly.

While the web app runs, it syncs every 6 hours by itself (the `AutoSyncHours` setting). To sync every day even
when the app is closed, install the scheduled task.

## Install the daily sync

From the repository folder, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-daily-sync.ps1
```

The script rebuilds the app in `%LOCALAPPDATA%\KbcTracker\app`, replacing any older build there, and registers
the Windows scheduled task "KbcTracker daily sync". The task runs every day at 12:40, or as soon as the PC is on.
Your data, one folder up, is not touched.

- Choose another time with `-Time 07:30`.
- See what would happen without changing anything with `-WhatIf`.

Run the script again after you update the app: the task keeps using the build it was installed with.

## Test it

Run the sync from the app folder, so it finds its `appsettings.json`:

```powershell
Push-Location "$env:LOCALAPPDATA\KbcTracker\app"
dotnet .\KbcTracker.Web.dll --sync
Pop-Location
```

It prints the number of new transactions, or the error. When transactions from the bank could not be read, it
also prints how many are left out; the app shows the same count until **Sync all again** finds none.

## Remove it

```powershell
Unregister-ScheduledTask -TaskName "KbcTracker daily sync" -Confirm:$false
```
