# Get started

In about 10 minutes you connect your KBC Brussels account and see your first month of transactions.

KbcTracker reads your account through Enable Banking, an aggregator for the Payment Services Directive 2 (PSD2).
Its free "restricted mode" only gives access to accounts you link yourself, and only to read them.

## What you need

- The .NET 10 SDK.
- A KBC Brussels account, with itsme or a card reader to log in.

## 1. Register an Enable Banking application

1. Sign up at <https://enablebanking.com/cp> (the Control Panel).
1. Open **Applications** and choose **Register new application**:
   - Environment: **Production**.
   - Redirect URL: `https://localhost:5001/callback`.
   - Let it generate the key. The `.pem` private key downloads; keep it outside the repository.
1. On the new application, which shows as *Inactive*, choose **Activate by linking accounts**.
1. Pick **KBC Brussels** and log in with itsme or a card reader. The application becomes *Active (restricted)*.

## 2. Configure the app

Open `src/Containers/UI/Hosting/KbcTracker.Web/appsettings.json` and fill in the `EnableBanking` section:

- `ApplicationId`: the application ID from the Control Panel.
- `PrivateKeyPath`: the full path of the `.pem` file.

The other settings are described in [Configuration](../reference/configuration.md).

## 3. Run the app

```powershell
dotnet dev-certs https --trust
dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web
```

You run the first command only once; it trusts the local HTTPS certificate.

## 4. Connect and download your transactions

1. Open <https://localhost:5001>.
1. Choose **Connect KBC Brussels** and log in again at KBC.
1. Wait for the download to finish. KBC returns about the last 90 days.

You now see the current month on the **Overview** tab.

## Next steps

- [Sync every day](../how-to/sync-every-day.md), even when the app is closed.
- Accept your fixed payments on the **Fixed payments** tab and confirm categories on **To review**
  (see [Tabs](../reference/tabs.md)).
