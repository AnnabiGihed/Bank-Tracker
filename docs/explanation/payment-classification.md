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
the longest matching keyword wins ("AUTOMOBILE" beats "CARREFOUR"). For card payments only the shop name is
searched, because the rest of the label contains words such as "Google Pay" or "KBC". Keyword categories wait on
the **To review** tab until you confirm them.

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
