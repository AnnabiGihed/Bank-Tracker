# Configuration

KbcTracker has two programs, each with its own `appsettings.json`, and the Aspire AppHost that starts them. Any
key can also be given on the command line (`--AutoSyncHours=0`) or as an environment variable (`AutoSyncHours=0`, nested keys with `__`). In Development
the backend also reads .NET user secrets; keep secrets there or in environment variables, never in a committed
file.

## Backend

The backend reads `src/Containers/API/KbcTracker.ApiService/appsettings.json`.

| Key | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings:KbcTracker` | none, required | The PostgreSQL connection string. Contains the database password, so keep it in user secrets or an environment variable. |
| `BackendPort` | `5080` | The port of the backend's listener, which is always on IPv4 loopback (`127.0.0.1`). |
| `AllowedHosts` | `localhost;127.0.0.1;[::1]` | The host names requests may use. Add the tailnet name when the backend is served through Tailscale. |
| `BackendAccess:OwnerLogin` | empty | Outside Development, the only `Tailscale-User-Login` value allowed in. Empty denies every data request. |
| `AutoSyncHours` | `6` | While the backend runs, sync when the last sync is older than this many hours; it checks every 30 minutes. `0` turns it off. |
| `EnableBanking:ApplicationId` | empty | The application ID from the Enable Banking Control Panel. |
| `EnableBanking:PrivateKeyPath` | empty | The full path of the application's `.pem` private key. |
| `EnableBanking:RedirectUrl` | `https://localhost:5001/callback` | Where the bank sends you back after login: the web app's callback. Must be registered in the Control Panel. |
| `EnableBanking:Country` | `BE` | The country of the banks offered. |
| `EnableBanking:ConsentDays` | `180` | How long the consent asked from the bank lasts, in days, from 1 to 180. |
| `EnableBanking:HistoryMonths` | `13` | How far back a full sync asks for. KBC returns about 90 days whatever you ask. |
| `EnableBanking:BaseUrl` | `https://api.enablebanking.com` | The Enable Banking API address. |
| `Categories` | built-in list | Category names with their keywords, used only by `--initialize` to create the first categories. |

## Web app

The web app reads `src/Containers/UI/Hosting/KbcTracker.Web/appsettings.json`.

| Key | Default | Meaning |
| --- | --- | --- |
| `Urls` | `https://localhost:5001` | Where the web app listens. Keep it equal to the host of `EnableBanking:RedirectUrl`. |
| `AllowedHosts` | `localhost;127.0.0.1;[::1]` | The host names requests may use. |
| `Backend:BaseUrl` | `http://127.0.0.1:5080/` | The backend's address, ending in a slash. |
| `Backend:OwnerLogin` | empty | Outside Development, the only `Tailscale-User-Login` value allowed in; it is also sent to the backend. |

## Aspire AppHost

The AppHost reads `src/Containers/Aspire/Hosting/KbcTracker.AppHost/appsettings.json` if present, its user secrets
and the environment. It sets `ConnectionStrings:KbcTracker`, `BackendPort` and `Backend:BaseUrl` for the programs
it starts, so leave those unset when you use it.

| Key | Default | Meaning |
| --- | --- | --- |
| `KbcTracker:JsonArchive` | `%LOCALAPPDATA%\KbcTracker` | The old JSON data folder that the dashboard's `import-json` reads. |
| `Parameters:postgres-password` | generated on the first run | The PostgreSQL password, kept in the AppHost's user secrets. The data volume is tied to it; never change or remove it. |

## Secrets

| Value | Secret | Where it lives |
| --- | --- | --- |
| The private key (`.pem`) | Yes | A file outside the repository; only its path is in `PrivateKeyPath`. `*.pem` files are git-ignored. |
| The database password | Yes | Under the AppHost, `Parameters:postgres-password` in the AppHost's user secrets. Without it, in `ConnectionStrings:KbcTracker`, in user secrets or an environment variable. |
| `ApplicationId` | No | User secrets or `appsettings.json`; requests are only accepted when signed with the private key. |

Command-line options are listed in [Command line](command-line.md).
