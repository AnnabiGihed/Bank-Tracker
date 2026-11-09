# Data folder

Everything the app stores lives in one folder: `%LOCALAPPDATA%\KbcTracker` by default, or the `DataDirectory`
setting. It survives rebuilds and updates. Nothing leaves your machine except the calls to Enable Banking and KBC.

| File | Content |
| --- | --- |
| `transactions.json` | Every transaction ever downloaded, newest first, with the bank's original JSON. Transactions are added or updated by id, never deleted. |
| `userdata.json` | Your decisions: accepted fixed payments, dismissed suggestions, categories per payee and per transaction, payee names. |
| `categories.json` | Your categories and their keywords. Created from the `Categories` setting the first time. |
| `session.json` | The bank connection: session, accounts, consent end date, last sync and its error. |
| `backups/transactions-yyyy-MM-dd.json` | A copy of the transactions, once a day before the first change; the last 30 are kept. |

Files are written to a temporary file first and then swapped in, so a crash never leaves a half-written file.

Data from versions that kept it next to the program (`bin\…\data`) is copied here on first start.
