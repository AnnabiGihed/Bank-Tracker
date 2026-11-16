# Add an account

The app syncs every account your consent covers. In Enable Banking's restricted mode, that is only the accounts you
linked in the Control Panel. To add another account, such as a savings account:

1. Open <https://enablebanking.com/cp>, open your application, and link the account.
1. In the app, open **Settings** and choose **Reconnect**.
1. At KBC, select every account you want to share, including the new one.

The next sync downloads the new account's transactions too.

## What the app does with the new account

At every reconnect the app keeps each linked account's IBAN, name and type, as KBC sends them. A connection made before
this existed has none until your next reconnect.

- **Own transfers:** a payment to or from one of your own IBANs is an own transfer the bank makes certain. It is no
  spending and no income. The account holder's name stays as the fallback.
- **Saved money:** a transfer from one account into a savings account counts as **Saved**. Money coming back from the
  savings account, and the matching credit on it, are ignored.
- **Which account is savings:** only an account KBC types `SVGS` is savings; a current account (`CACC`) never is. When
  the type is missing nothing is guessed: the transfer stays an ordinary own transfer, and money leaving a savings
  account is never saved. Correct one payment by choosing another category for it.
- **Same bank reference in two accounts:** a bank reference is unique within one account only. When a transaction of the
  new account has a reference another account already uses, the app stores it as `<account id>:<reference>`. Stored
  transactions keep their ids.
  A collision that happened before this version (only possible when two accounts were already synced) is not repaired;
  a full resync after reconnecting restores it.
