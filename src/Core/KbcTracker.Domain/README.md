# KbcTracker.Domain

The domain of KbcTracker. It depends on no other project or package.

| Feature | Types |
| --- | --- |
| `Features/Transactions` | `Txn` (a stored transaction), `Kind`, `TxnInfo`, `TxnPart` (the share of a payment in one category), and the internal `BankFields` that reads the ISO code from `Txn.Raw` |
| `Features/Categories` | `Category` (with its parent and COICOP code), `CategoryType`, `PayeeAssignment`, `CategoryTree` (navigation, roll-up and the tree rules), `CategoryDefaults` and its seed `CategoryTreeSeed` |
| `Features/FixedPayments` | `RecurringPayment`, `Suggestion`, `FixedRow`, `FixedStatus`, `SubscriptionRule` (card, monthly or yearly) |
| `Features/BankConnection` | `SessionInfo` |
| `Features/Overview` | `MonthSummary` |
| `Features/Review` | `ReviewItem` |
| `Features/Splits` | `PaymentSplit` (a payment divided over categories, ADR-0007), `SplitLine`, `PaymentSplitErrors` |
| `Features/Debts` | `Debt` (money borrowed on `BorrowedOn`, with the payments the owner added or excluded), `RepaymentPlan` (one payment or instalments, with the derived count or amount and the schedule), `DebtTerms`, `DebtLedger` (matches repayments and works out each `DebtStatus`, ADR-0010), `DebtState`, `ScheduleRow`, `DebtErrors`, `RepaymentPlanErrors` |
| `Features/Shared` | `UserData` (every user decision) and `Analysis`, the domain service that classifies and summarises |

The rules `Analysis` applies are explained in
[How payments are classified](../../../docs/explanation/payment-classification.md).
Tests: `test/Core/KbcTracker.Domain.Tests`.
