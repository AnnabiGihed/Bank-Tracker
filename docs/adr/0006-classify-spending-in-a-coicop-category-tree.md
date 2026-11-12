# ADR-0006: Classify spending in a COICOP 2018 category tree by purpose

- Status: Proposed
- Date: 2026-11-11
- Deciders: Gihed Annabi

## Context

Categories were one flat list: ten built-ins the analysis relies on (`income`, `savings`, `loan`, `cash`, …) and
thirteen seeded ones such as "Transport & fuel" and "Groceries". The owner wants categories that match the real world,
with sub-categories at several levels (Transport › Running own vehicles › Car › Fuel), seeded by default and
extendable at any level. The app is Belgian today but is meant to work in other countries later.

Forces:

- The bank only sees "DELHAIZE −€84.30", never the receipt, and KBC sends no merchant category code: in the owner's
  318 synced transactions, Enable Banking's `merchant_category_code` is present but empty in every one, card payments
  included. The merchant must therefore be recognised by its name.
- A payment can often be placed only partly: a STIB charge is public transport, but not which of bus, tram or metro;
  a Brussels pass covers several modes at once.
- Later steps (splitting a payment, receipts read on the phone, instant payment notifications) need a tree to put the
  parts in.
- Charts must stay readable: the owner wants the top level first and one level more per click.

Options considered:

1. Keep a flat list and add more categories.
1. A tree designed for this app.
1. A tree based on COICOP 2018, the UN classification of household spending by purpose, which Statbel and every
   national statistics office use.

## Decision

We classify spending in a category tree based on COICOP 2018 (option 3), with friendly names.

- **By purpose, not by shop.** Shampoo is Personal care and car insurance is Insurance, wherever they were bought. A
  supermarket payment without a receipt lands on Food & non-alcoholic drinks.
- **Structure.** The thirteen COICOP divisions are the spending roots, with their groups and the owner's own levels
  below them, plus roots outside COICOP for what is not spending: Income, Savings & investments, Own transfers, Loan
  repayments, Taxes & government, Gifts & donations, Cash withdrawals, Transfers to others, Unsorted direct debits and
  Other. Each node stores its COICOP division or group code (verified on the INSEE COICOP 2018 listing); class codes
  are not stored.
- **At most four levels.** A child has its root's type (expense, income, savings, transfer), so totals by type stay
  correct.
- **A payment stays on the deepest node that is truly known.** When that is a parent, it is shown as "not specified"
  under that parent until the owner moves it or sets a rule for the payee. Totals roll up: a node counts its own
  payments and all its descendants'.
- **Built-in ids stay** and cannot be deleted, because the analysis assigns them directly. Ids of the earlier flat
  defaults stay where the concept survives (`groceries`, `car`, `housing`, `health`, `insurance`,
  `restaurants-cafes`, `shopping`). Deleting a node moves its payments and rules to its parent.
- **Keywords in layers.** A worldwide core (global brands and generic shop words in French, Dutch, English and
  German) is always seeded; a country pack (Belgium first) is added for the country in `EnableBanking:Country`. The
  longest matching keyword wins, and on a tie the deeper category.
- **Existing databases** are upgraded once, while no category has a parent: the tree is added around the existing
  categories and the three flat defaults that have no single counterpart (`transport-fuel`, `subscriptions`,
  `utilities-telecom`) are merged: each payee rule, payment choice and fixed payment that pointed to one of them is
  moved to the node whose keyword matches its payee (falling back to Transport, Streaming & subscriptions and Unsorted
  direct debits), and a payee rule moved this way becomes unconfirmed so it shows up in To review. The upgrade runs
  only while one of those three ids still exists, so a tree the owner flattened later is not seeded again.

## Consequences

- The tree matches official spending statistics, so a later version can compare the owner's spending with Belgian
  averages and work in other countries without redesigning the categories.
- Supermarket spending stays on its root until payments can be split; the detailed food and household nodes fill from
  specialised shops, the owner's choices, and later from receipts.
- Shop names still need a country pack for local chains, operators and utilities; a merchant-lookup service could
  replace that later, at the cost of sending merchant names outside.
- Charts, pickers, the Categories tab and the transaction filter must understand the tree.
- The upgrade only recognises the earlier flat defaults; categories the owner created stay as roots.

## Revisit when

- Payments can be split, or receipts are read: check the leaf nodes against real receipts.
- The app is used outside Belgium: add that country's pack, or a merchant-lookup service.
- A bank that sends merchant category codes is connected: map ISO 18245 codes onto the tree.
