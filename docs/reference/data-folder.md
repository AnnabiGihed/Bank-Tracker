# Stored data

The backend keeps everything in its PostgreSQL database. Nothing leaves your PC except the calls to Enable Banking
and KBC. The web app stores nothing.

| Table | Content |
| --- | --- |
| `Transactions` | Every transaction ever downloaded, with the bank's original JSON. Transactions are added or updated by id, never deleted. |
| `Categories` | Your categories and their keywords. |
| `UserData` | Your decisions: accepted fixed payments (in their own table), dismissed suggestions, categories per payee and per transaction, payee names. |
| `BankSessions` | The bank connection: session, accounts, consent end date, last sync and its outcome. |

Back up the database before you apply migrations. Automated backups come with the Docker Compose setup
([ADR-0004](../adr/0004-serve-both-apps-from-one-postgresql-backend-and-api.md)); until then, use `pg_dump`.

## The old JSON data folder

Versions before the backend kept their data in JSON files in `%LOCALAPPDATA%\KbcTracker` (or the old
`DataDirectory` setting). The backend imports that folder once with `--import-json` (see
[Command line](command-line.md)) and never writes to it. Keep it as an archive.

| File | Content | Import |
| --- | --- | --- |
| `transactions.json` | Every transaction, with the bank's original JSON. | Required. |
| `categories.json` | Your categories and their keywords. | Required. |
| `userdata.json` | Your decisions. | Optional: the old app wrote it at your first decision, so without it you have none. |
| `session.json` | The bank connection. | Optional. |
| `backups/` | Daily copies of the transactions. | Not imported. |
