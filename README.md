# KbcTracker — your KBC Brussels transactions, fixed payments & spending dashboard

Runs locally on your PC. Data is pulled read-only from KBC Brussels through
Enable Banking (free "restricted mode" for your own accounts) and stored in `./data`
next to the app — nothing leaves your machine except the calls to Enable Banking/KBC.

## One-time setup (≈10 min)
1. Sign up at https://enablebanking.com/cp (Control Panel).
2. Applications → **Register new application**
   - Environment: **Production**
   - Redirect URL: `https://localhost:5001/callback`
   - Let it generate the key → the `.pem` private key downloads. Keep it safe.
3. On the new (Inactive) application click **Activate by linking accounts**,
   pick **KBC Brussels**, log in with itsme / card reader. App becomes *Active (restricted)*.
4. Edit `appsettings.json`: put the **Application ID** and the path to the `.pem`.
5. `dotnet dev-certs https --trust` (once), then `dotnet run`.
6. Open https://localhost:5001 → **Connect KBC Brussels** → log in again → transactions download.

## Day to day
- Open the dashboard and click **Sync now** (or call `/sync` from a scheduled task).
- Consent lasts up to 180 days; the header warns you 15 days before — click **Reconnect**.
- **Export CSV** gives every transaction with category and fixed/variable flag.

## Tuning
- Categories: keyword lists in `appsettings.json` → `Categories`.
- Fixed-payment detection (`Analysis.cs`): same payee in ≥3 months (2 if less data),
  about once a month, amount stable within ±15%. Flags missed payments and price changes.
