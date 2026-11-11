# Command line

Run these commands from the repository folder.

| Command | Effect |
| --- | --- |
| `dotnet build` | Builds the solution (`KbcTracker.slnx`). |
| `dotnet test` | Runs the tests. |
| `dotnet run --project src/Containers/API/KbcTracker.ApiService` | Starts the backend at <http://127.0.0.1:5080>. |
| `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web` | Starts the web app at <https://localhost:5001>. Start the backend first. |
| `dotnet run --project src/Containers/API/KbcTracker.ApiService -- --AutoSyncHours=0` | Starts the backend with automatic syncs off; any setting can be given this way. |

## Backend maintenance commands

Each command runs once and exits without starting the API. Give one command at a time, after `--`, for example
`dotnet run --project src/Containers/API/KbcTracker.ApiService -- --migrate`.

| Command | Effect |
| --- | --- |
| `--migrate` | Applies the database migrations. Normal startup never migrates. |
| `--import-json <folder>` | Imports a JSON data folder of an older version: transactions, categories, your decisions and the bank connection. The folder is only read. Refused when the database already holds data. |
| `--initialize` | Starts with no history: the default categories, defined in the code, and no decisions. A database that already holds data is left alone, and the command still succeeds. |
| `--sync` | Syncs every connected account once. |

Until you run `--import-json` or `--initialize`, the backend refuses decisions and new bank connections with
`409 Conflict`, so that the import stays possible.

| Exit code | Meaning |
| --- | --- |
| 0 | Done. `--sync` prints `Sync completed: <n> new transactions.` |
| 1 | The command failed or was refused; the message says why. Nothing was half-imported. |
| 2 | The arguments were not one of the commands above. |

The web app has no commands of its own. Its former `--sync` option now exits with code 2 and points to the
backend's `--sync`.
