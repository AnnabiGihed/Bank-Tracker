# KbcTracker.Infrastructure

External services of KbcTracker. It implements the application's ports.

| Feature | Types |
| --- | --- |
| `Features/BankConnection` | `EnableBankingClient` (REST, JWT signed with the private key), `EnableBankingOptions`, `EnableBankingMapper` (JSON to `Txn`), `EnableBankingTransactionSource` (the `IBankTransactionSource` implementation) |

The settings are described in [Configuration](../../../docs/reference/configuration.md).
Tests: `test/Infrastructure/KbcTracker.Infrastructure.Tests`.
