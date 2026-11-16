# ADR-0010: Track debts and their repayment plans

- Status: Proposed
- Date: 2026-11-16
- Deciders: Gihed Annabi

## Context

Money in from a bank loan lands in Loan received, but money borrowed from a friend is counted as income, and nothing
records how either is paid back. The owner wants borrowed money kept apart from income, the repayment plan asked for when
a debt appears, and the progress followed.

Forces:

- KBC returns about 90 days, so a loan paid out earlier has no credit in the history: a debt cannot require one.
- Bank loan instalments are already detected (`CSLN/PPAY`, series `LOAN:<ref>`); repayments to a person are ordinary
  transfers, identified by IBAN or, failing that, by name.
- User decisions live in `UserData`; splits (ADR-0007) are stored as versioned jsonb.
- The owner wants one behaviour for personal and bank debts: one payment on a date, or instalments with a start date,
  a frequency, a day of the month and either the amount per instalment or the number of instalments.

Options considered:

1. Notes on the payment only (a category and free text).
1. A `Debt` decision in `UserData` with a repayment plan, repayments matched automatically and correctable.
1. A separate debts aggregate with its own table.

## Decision

We record each debt as a user decision in `UserData` with its repayment plan (option 2).

- **Debt:** kind (personal or bank), lender name and optional IBAN, optional received payment, the amount received, the
  day it was borrowed (`BorrowedOn`; only payments on or after it can repay the debt), a plan,
  an optional `LOAN:` series for bank instalments, and the payments the owner added or excluded by hand.
- **Plan:** one payment on a date, or instalments (weekly, monthly, quarterly or yearly; day of the month capped at the
  month's end); the owner gives the instalment amount or the count and the other is derived; the last instalment may
  differ; the total to repay may include interest.
- **Matching:** debits on or after `BorrowedOn`, oldest first, until the debt is covered: the `LOAN:` series for a bank loan,
  transfers to the lender's IBAN (or name without an IBAN) for a person. A payment counts for one debt only; manual
  additions always count and exclusions never do.
- **Totals:** repayments matched to a debt and bank-loan instalments are never spending.
- **Category:** a built-in "Borrowed from people" under Loan received (Transfer type), never income.
- **Storage:** a versioned jsonb column on `UserData`, like the splits; changes go through decisions (`addDebt`,
  `updateDebt`, `removeDebt`, `matchRepayment`, `unmatchRepayment`, `resetRepayment`).
- **UI:** a plan dialog when a credit becomes Loan received or Borrowed (it can be skipped), a Debts tab with each
  debt's schedule and progress, and an Overview notice for due-soon, overdue and unplanned debts.

## Consequences

### Positive

- Borrowed money never inflates income, and repayments never inflate spending.
- The owner sees what is left, what is next and what is late, for friends and banks alike.
- Repayments are found automatically and every match can be corrected.

### Negative

- Matching a friend by name can pick the wrong transfer; an IBAN avoids it and matches can be corrected.
- Instalments that change amount and repayments in cash need manual corrections.
- Loans older than the history need their plan entered by hand.
- A database migration on the owner's data, preceded by a backup.

## Alternatives considered

- **Notes on the payment** - rejected: no schedule, no progress, no warnings.
- **A separate aggregate and table** - rejected: debts are user decisions like splits; nothing queries them on their
  own, and a second aggregate would need its own concurrency.
