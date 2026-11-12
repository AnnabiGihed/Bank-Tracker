# KbcTracker.Domain

The domain of KbcTracker. It depends on no other project or package.

| Feature | Types |
| --- | --- |
| `Features/Transactions` | `Txn` (a stored transaction), `Kind`, `TxnInfo`, and the internal `BankFields` that reads the ISO code from `Txn.Raw` |
| `Features/Categories` | `Category` (with its parent and COICOP code), `CategoryType`, `PayeeAssignment`, `CategoryTree` (navigation, roll-up and the tree rules), `CategoryDefaults` and its seed `CategoryTreeSeed` |
| `Features/FixedPayments` | `RecurringPayment`, `Suggestion`, `FixedRow`, `FixedStatus` |
| `Features/BankConnection` | `SessionInfo` |
| `Features/Overview` | `MonthSummary` |
| `Features/Review` | `ReviewItem` |
| `Features/Shared` | `UserData` (every user decision) and `Analysis`, the domain service that classifies and summarises |

The rules `Analysis` applies are explained in
[How payments are classified](../../../docs/explanation/payment-classification.md).
Tests: `test/Core/KbcTracker.Domain.Tests`.
