# KbcTracker.Persistence.EntityFrameworkCore

Implements the domain repositories and the application's storage ports on PostgreSQL through Pivot.Framework's
EF Core context, unit of work and advisory locks. The migrations map the permanent history, categories, bank
connections and user decisions. `DataImporter` writes a whole snapshot (an imported archive or a fresh start) and
refuses a database that already holds data. `CategoryTreeUpgrade` turns the flat category list of an older database
into the category tree once, from `--initialize`. The user's decisions are one row; the payee categories, the choices
for single payments, the splits of payments (`TxnSplits`), the debts with their repayment plans (`Debts`, a versioned
`StoredDebt` array, version 2 adds the day borrowed; a version-1 debt reads with its plan's first date, ADR-0010) and the payee names are `jsonb` columns, converted in `JsonColumns`. A stored category
tree without the built-in `borrowed-people` gets it when the workspace is read.

See [Run the backend](../../../docs/how-to/run-the-backend.md) for the migration, import and initialisation
commands. Tests use a disposable real PostgreSQL database; see [Run the tests](../../../docs/how-to/run-the-tests.md).
