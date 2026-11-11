# Get started

In about 20 minutes you set up the backend, connect your KBC Brussels account and see your first month of
transactions.

KbcTracker reads your account through Enable Banking, an aggregator for the Payment Services Directive 2 (PSD2).
Its free "restricted mode" only gives access to accounts you link yourself, and only to read them.

## What you need

- The .NET 10 SDK.
- A PostgreSQL database you can reach from your PC, for example in Docker.
- The Pivot.Framework feed variables `PIVOT_PACKAGES_USER` and `PIVOT_PACKAGES_TOKEN` (see the root README).
- A KBC Brussels account, with itsme or a card reader to log in.

## 1. Register an Enable Banking application

1. Sign up at <https://enablebanking.com/cp> (the Control Panel).
1. Open **Applications** and choose **Register new application**:
   - Environment: **Production**.
   - Redirect URL: `https://localhost:5001/callback`. The bank sends you back to the web app, which hands the
     result to the backend.
   - Let it generate the key. The `.pem` private key downloads; keep it outside the repository.
1. On the new application, which shows as *Inactive*, choose **Activate by linking accounts**.
1. Pick **KBC Brussels** and log in with itsme or a card reader. The application becomes *Active (restricted)*.

## 2. Configure and prepare the backend

Follow [Run the backend](../how-to/run-the-backend.md) up to and including "Prepare the database":

1. Store the database connection string, the application ID and the private-key path as user secrets.
1. Apply the migrations.
1. Start fresh with `--initialize`. If you used an older version that kept its data in JSON files, import that
   folder with `--import-json` instead, so you keep your history and decisions.

The other settings are described in [Configuration](../reference/configuration.md).

## 3. Run the backend and the web app

In one terminal, start the backend:

```powershell
dotnet run --project src/Containers/API/KbcTracker.ApiService
```

In a second terminal, start the web app:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
```

You run `dotnet dev-certs https --trust` only once; it trusts the local HTTPS certificate.

## 4. Connect and download your transactions

1. Open <https://localhost:5001>.
1. Choose **Connect KBC Brussels** and log in again at KBC.
1. Wait for the download to finish. KBC returns about the last 90 days.

You now see the current month on the **Overview** tab.

## Next steps

- [Keep syncing every day](../how-to/sync-every-day.md): KBC only returns about 90 days.
- Accept your fixed payments on the **Fixed payments** tab and confirm categories on **To review**
  (see [Tabs](../reference/tabs.md)).
