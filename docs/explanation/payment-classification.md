# How payments are classified

KbcTracker decides what each transaction is, which payee it belongs to, which category it gets and whether it is
a fixed payment. This page explains the reasoning; the code is `Analysis` in `src/Core/KbcTracker.Domain`.

## What kind of payment

KBC sends a bank transaction code from the International Organization for Standardization (ISO) with each
transaction (`bank_transaction_code.code/sub_code`). The app trusts it more than the text:

- **Direct debits** (`IDDT`): one payee per mandate, so two contracts with the same company stay separate.
- **Loan instalments** (`CSLN`), **savings plans** (`SETT/SUBS`), **account fees** (`OPCL`) and spare-change
  investing.
- **Card payments** (`CCRD`): grouped by shop name, cleaned from the bank label.
  `WL*GOOGLE YouTube IED02 R296 Dublin` and `GOOGLE *YouTube IEUNKNOWN` both become *Google YouTube*.
- **Transfers**: grouped by the other party's International Bank Account Number (IBAN).
- **Loan payouts** (`RCDT/CAJT`) are not income.

The account holder is the name that most often pays money out. Transfers to or from that name are own transfers:
they count neither as spending nor as income.

## Which category

The first rule that applies wins:

1. The category you chose for this one transaction.
1. The category you chose for the payee.
1. The category of the fixed payment the transaction belongs to.
1. An automatic category.

Automatic categories come from the bank code when it makes them certain, and need no confirmation:

- loan;
- own transfer;
- fee;
- savings plan;
- cash.

Otherwise a keyword decides: a keyword matches only at the start of a word, and
the longest matching keyword wins ("AUTOMOBILE" beats "CARREFOUR"); on a tie, the deeper category wins. For card
payments only the shop name is searched, because the rest of the label contains words such as "Google Pay" or "KBC".
Keyword categories wait on the **To review** tab until you confirm them.

## Split payments

You can split one payment into parts, for example a supermarket receipt into meat, bread and toiletries
([ADR-0007](../adr/0007-split-a-payment-into-categorised-parts.md)). A split is your decision, kept apart from the
bank's transaction, so a new sync never removes it.

- **A split beats every other category.** The category of the payment itself, of its payee and of its fixed payment
  stay what they would be without the split, so splitting one visit never teaches the app that a shop is "Meat".
  Removing the split brings the payment's category back.
- **Parts carry the money.** Every total by category adds up parts, not payments: a payment that is not split has
  one part with its whole amount. A split payment counts as spending and as confirmed, so it leaves **To review**.
- **Parts must add up.** A split has 2 to 100 parts, each an amount above zero with at most two decimals, in a spending
  category, adding up exactly to the payment. Only a spending payment or a refund (a payment in a spending category)
  can be split, and a refund's parts reduce spending.
- **A split that no longer adds up** (the bank changed the amount) keeps its parts and gets one more part,
  "Difference", on the payment's own category (or Other when that is no longer spending), so totals always match the
  bank.
- **Deleting a category** moves the parts that use it to its parent, as for your other decisions. A category used by
  parts (or whose sub-category is) cannot stop counting as spending until those parts are moved.

## The category tree

Categories form a tree of at most four levels, based on COICOP 2018, the United Nations classification of household
spending by purpose ([ADR-0006](../adr/0006-classify-spending-in-a-coicop-category-tree.md)). Its thirteen divisions
are the spending roots, such as Food & non-alcoholic drinks, Transport and Health; roots outside COICOP hold what is
not spending, such as Income, Savings & investments and Own transfers.

- **By purpose, not by shop.** Shampoo is Personal care and car insurance is Insurance, wherever they were bought.
- **A child has its root's type**, so its payments count as spending, income, savings or a transfer like its root.
- **A payment stays on the deepest category that is known.** A STIB payment lands on Public & paid transport,
  because it does not say whether it was a bus, a tram or the metro. It shows as "not specified" under that category
  until you move it or set a category for the payee.
- **Totals roll up**: a category counts its own payments and those of all its sub-categories.
- **Keywords come in layers.** A worldwide core (global brands, and shop words in French, Dutch, English and German)
  is always seeded; a country pack, Belgium for now, adds local chains, transport operators and utilities. KBC sends
  no merchant category code, so the shop is recognised by its name.

## Fixed-payment suggestions

Payments are grouped by payee. Payments of different payees with almost the same amount (±3 %) and a distinctive
shop word in common are grouped too, so a changed bank label does not split a subscription.

A group is suggested when it comes back on a regular rhythm with a stable amount. The rhythm is monthly, quarterly
or yearly.

Some payments are contracts by nature, and are suggested as soon as they appear in two months:

- direct debits;
- loans;
- savings plans;
- fees.

Suggestions are never accepted for you. Once you accept one, new payments are matched to it by payee, or by its
shop words and an amount within its tolerance.

KBC only returns about 90 days of history. A quarterly payment is therefore seen at most twice, and a yearly one
once, until the stored history grows.
