# Configuration

The web app reads `src/Containers/UI/Hosting/KbcTracker.Web/appsettings.json`. Any key can also be given on the
command line (`--AutoSyncHours=0`) or as an environment variable (`AutoSyncHours=0`, nested keys with `__`).

## Settings

| Key | Default | Meaning |
| --- | --- | --- |
| `Urls` | `https://localhost:5001` | Where the app listens. Keep it equal to the host of `EnableBanking:RedirectUrl`. |
| `DataDirectory` | empty | The data folder; empty means `%LOCALAPPDATA%\KbcTracker`. Environment variables are expanded. See [Data folder](data-folder.md). |
| `AutoSyncHours` | `6` | While the app runs, sync when the last sync is older than this many hours. `0` turns it off. |
| `EnableBanking:ApplicationId` | empty | The application ID from the Enable Banking Control Panel. |
| `EnableBanking:PrivateKeyPath` | empty | The full path of the application's `.pem` private key. |
| `EnableBanking:RedirectUrl` | `https://localhost:5001/callback` | Where the bank sends you back after login. Must be registered in the Control Panel. |
| `EnableBanking:Country` | `BE` | The country of the banks offered. |
| `EnableBanking:ConsentDays` | `180` | How long the consent asked from the bank lasts, in days (180 at most). |
| `EnableBanking:HistoryMonths` | `13` | How far back a full sync asks for. KBC returns about 90 days whatever you ask. |
| `EnableBanking:BaseUrl` | `https://api.enablebanking.com` | The Enable Banking API address. |
| `Categories` | built-in list | Category names with their keywords, used only to create `categories.json` the first time. |

## Secrets

| Value | Secret | Where it lives |
| --- | --- | --- |
| The private key (`.pem`) | Yes | A file outside the repository; only its path is in `PrivateKeyPath`. `*.pem` files are git-ignored. |
| `ApplicationId` | No | `appsettings.json`; requests are only accepted when signed with the private key. |

Command-line options are listed in [Command line](command-line.md).
