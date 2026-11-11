# KbcTracker.Infrastructure

External services of KbcTracker. It implements the application's bank ports.

| Feature | Types |
| --- | --- |
| `Features/BankConnection` | `EnableBankingClient` (REST, JWT signed with the private key), `EnableBankingOptions`, `EnableBankingMapper` (JSON to `Txn`), `EnableBankingTransactionSource` and `EnableBankingConsentSource` (the bank ports) |
| `Features/Legacy` | `LegacyJsonReader`, which reads the JSON data folder of older versions for the one-time import, and the `Documents` that map its file shapes to the domain |

The settings are described in [Configuration](../../../docs/reference/configuration.md).
Tests: `test/Infrastructure/KbcTracker.Infrastructure.Tests`.
