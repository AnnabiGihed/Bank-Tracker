# ADR-0007: Split a payment into categorised parts

- Status: Proposed
- Date: 2026-11-12
- Deciders: Gihed Annabi

## Context

The bank sees one amount per payment: "DELHAIZE −€84.30". ADR-0006 puts such a supermarket payment on Food &
non-alcoholic drinks, but the owner knows that €50 was meat, €20 bread and €14.30 toiletries, and wants the totals and
the drill-down charts to show that. Later, receipts read on the phone will fill the same information automatically.

Forces:

- The bank's transaction is re-synced and must never be changed by the app (ADR-0004: user decisions live apart from
  the bank data).
- The analysis also uses a payment's category to learn about its payee: the majority category of a suggested fixed
  payment, the category confirmed for a payee, the review queue. One supermarket visit should not teach the app that
  Delhaize is "Meat".
- A receipt is a list of lines with an amount, a description and, once read, a category. Discounts and deposit
  returns appear as negative lines; a large receipt can have more than a hundred lines.
- Totals by type (spending, income, savings) must stay correct, and a payment whose amount no longer matches its parts
  must never make a total disagree with the bank.

Options considered:

1. Store a list of category amounts on the transaction itself.
1. Store the split as a user decision next to the per-transaction category, and let the analysis turn it into parts.
1. Model splits as separate child transactions.

## Decision

We store a split as a user decision (option 2): `UserData` keeps, per transaction id, a `PaymentSplit` of
`SplitLine`s, each with an amount, a category and an optional label, the shape of a receipt line.

- **Validation.** A split has 2 to 100 lines, each amount positive with at most two decimals, and the amounts add up
  exactly to the payment's amount (without its sign). Every line's category must exist.
- **Spending only, for now.** Every line uses an expense category, so a debit's parts are spending and a refund's parts
  reduce spending, as an unsplit refund does. Parts in savings or income categories (a loan instalment's capital and
  interest, for example) need their own decision.
- **Precedence.** A split wins over the payment's own category, the payee's, the fixed payment's and the automatic
  one. Removing the split brings back whatever category the payment had before.
- **Category and parts.** The payment's category stays the one it would have without the split, so payee rules,
  suggestions and the review queue are not influenced by one visit. The money is carried by the payment's parts: the
  split's lines, or one part with the whole amount when it is not split. Every total by category, the category
  roll-up, the category filter and the export use the parts; a split payment counts as spending and as confirmed.
- **Deleted categories.** Deleting a category moves the lines that use it to its parent (or `other` for a top-level
  category), as for the other decisions.
- **Mismatch.** When a stored split no longer adds up to the payment's amount, its lines are kept and one more part,
  "Difference", carries the gap on the payment's own category, so totals always match the bank and the owner can
  correct the split.
- **Receipts.** Lines stay positive: the receipt reader nets a discount or deposit return into the line it belongs
  to, and combines lines per category (keeping their labels) when a receipt has more than 100 lines. Where a split
  came from (typed or read from a receipt) can be added to the stored lines later without a schema change.

## Consequences

- The bank's transaction is untouched and a re-sync never loses a split.
- Supermarket spending can reach the detailed food and household nodes of ADR-0006 today by hand, and later from
  receipts.
- Every place that adds money up by category must read the parts, not the payment's category; a new chart or report
  has to follow that rule.
- The Transactions tab needs an editor whose parts must add up to the payment.
- Splitting into savings or income categories is not possible yet.

## Revisit when

- Receipts are read on the phone: check the 100-line limit, discounts and where a split came from.
- The owner wants to split a payment into savings or income parts, such as a loan instalment.
