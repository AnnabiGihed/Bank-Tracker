# KbcTracker documentation

KbcTracker reads your KBC Brussels transactions read-only through Enable Banking, keeps them forever, finds your
fixed payments and shows your spending per month. A backend on your PC holds the data in PostgreSQL and serves it
through an API; the web app shows it.

## Where to start

- New here? Follow [Get started](tutorials/get-started.md) to set up the backend, connect your account and see
  your first month.
- Already set up? Start the backend, then the web app, as in [Run the backend](how-to/run-the-backend.md), and
  open <https://localhost:5001>.

## Guides

| Type | Pages |
| --- | --- |
| Tutorial | [Get started](tutorials/get-started.md) |
| How-to | [Run the backend](how-to/run-the-backend.md), [Keep syncing every day](how-to/sync-every-day.md), [Reconnect the bank](how-to/reconnect-the-bank.md), [Add an account](how-to/add-an-account.md), [Run the tests](how-to/run-the-tests.md) |
| Reference | [Configuration](reference/configuration.md), [Command line](reference/command-line.md), [Tabs](reference/tabs.md), [Stored data](reference/data-folder.md), [API contract](api/openapi.json) |
| Explanation | [How payments are classified](explanation/payment-classification.md), [Architecture](explanation/architecture.md) |
| Decisions | [Architecture Decision Records](adr/README.md) |
