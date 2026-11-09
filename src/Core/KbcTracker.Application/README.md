# KbcTracker.Application

The use cases of KbcTracker. It depends only on `KbcTracker.Domain`.

| Feature | Types |
| --- | --- |
| `Features/BankConnection` | `SyncService` (downloads every connected account), the `IBankTransactionSource` port, `SyncSettings` |
| `Features/Shared` | The `IStore` port: session, transactions, user decisions and categories; transactions are never deleted |

The use cases move to Pivot.Framework's mediator commands and queries in step 4 of
[ADR-0003](../../../docs/adr/0003-build-on-pivot-framework-running-on-the-phone.md).
Tests: `test/Core/KbcTracker.Application.Tests`.
