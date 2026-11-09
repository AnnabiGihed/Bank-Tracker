# KbcTracker documentation

KbcTracker reads your KBC Brussels transactions read-only through Enable Banking, keeps them forever, finds your
fixed payments and shows your spending per month.

## Where to start

- New here? Follow [Get started](tutorials/get-started.md) to connect your account and see your first month.
- Run the app: `dotnet run --project src/Containers/UI/Hosting/KbcTracker.Web`, then open <https://localhost:5001>.

## Guides

| Type | Pages |
| --- | --- |
| Tutorial | [Get started](tutorials/get-started.md) |
| How-to | [Sync every day](how-to/sync-every-day.md), [Reconnect the bank](how-to/reconnect-the-bank.md), [Add an account](how-to/add-an-account.md), [Run the tests](how-to/run-the-tests.md) |
| Reference | [Configuration](reference/configuration.md), [Command line](reference/command-line.md), [Tabs](reference/tabs.md), [Data folder](reference/data-folder.md) |
| Explanation | [How payments are classified](explanation/payment-classification.md), [Architecture](explanation/architecture.md) |
| Decisions | [Architecture Decision Records](adr/README.md) |
