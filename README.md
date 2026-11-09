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

## Transaction history
- The first sync after connecting uses Enable Banking's `strategy=longest`: it asks KBC for the
  oldest transaction it will release and downloads everything after it.
- Banks usually release more than 90 days only right after you log in (strong authentication).
  If **Full re-sync** still returns only recent data, click **Reconnect**, log in, and the full
  history is pulled immediately afterwards.
- Everything is stored in `./data`, and daily syncs only add to it, so your history keeps growing
  even when the bank's window is short.

## Tuning
- Categories: keyword lists in `appsettings.json` → `Categories`.
- Fixed-payment detection (`Analysis.cs`), in this order:
  1. your own rules in `appsettings.json` → `FixedPayments` (`Match` = text, alternatives with `|`);
  2. same payee (IBAN, else name) in ≥3 months, about once a month, amount within ±15%;
  3. same amount (±2%) around the same day each month — catches loans whose description changes monthly.
- **Search** page: find any transaction by text, IBAN or amount and see the raw bank data,
  to write a `FixedPayments` rule.
