# Command line

Run these commands from the repository folder.

| Command | Effect |
| --- | --- |
| `dotnet build` | Builds the solution (`KbcTracker.slnx`). |
| `dotnet test` | Runs the tests. |
| `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web` | Starts the web app at <https://localhost:5001>. |
| `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web -- --sync` | Syncs once and exits. |
| `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web -- --AutoSyncHours=0` | Starts the web app with automatic syncs off; any setting can be given this way. |

## Sync mode

With `--sync`, the app syncs every connected account once and exits:

| Outcome | Output | Exit code |
| --- | --- | --- |
| Success | `OK, <n> new transactions`, followed by `, <k> could not be read and were left out` when some were | 0 |
| Failure | The error message | 1 |
| Transactions stored, but the sync result could not be saved | `<n> new transactions stored, but: <error>` | 1 |

The daily scheduled task runs the published app this way: `dotnet KbcTracker.Web.dll --sync`, from
`%LOCALAPPDATA%\KbcTracker\app`.

## Install script

`scripts/install-daily-sync.ps1` installs the daily sync.

| Parameter | Default | Meaning |
| --- | --- | --- |
| `-Time` | `12:40` | The time of the daily sync, as `HH:mm`. |
| `-WhatIf` | off | Shows what the script would do, without changing anything. |
| `-Confirm` | off | Asks before each step. |
