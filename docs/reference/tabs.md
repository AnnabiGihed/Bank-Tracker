# Tabs

Pick a month at the top, with ‹ › or the drop-down. Every tab follows it (`?m=yyyy-MM` in the address). The web app
and the Android app show the same tabs; the addresses are the web app's.

## Common to every tab

- Edits open in a dialog. Messages, errors and confirmations appear as notifications.
- After you change a payee's category, a notification offers **Only this one**: select it to undo the change for the
  payee and keep it for this payment only.
- Category pickers have a search box: type part of a name to find it. Every top-level category is a bold heading
  you can pick, with its sub-categories indented below it.
- Charts work offline. Each chart has a table of the same numbers for keyboard and screen-reader use.

## The tabs

| Tab | What it shows | What you can do |
| --- | --- | --- |
| **Overview** | Income, spending (fixed and variable), savings, what is left, the last months, spending by category, fixed payments still to come, the biggest expenses. A banner tells you when something needs your attention. | The "Where it went" chart shows the top-level categories. Select a slice to drill down into its sub-categories (a "not specified" slice holds payments that stay on the category itself); the breadcrumb above the chart goes back up. A slice without sub-categories opens its transactions. |
| **Fixed payments** | Suggestions found in your history, then your accepted fixed payments with their status this month: paid, due, late or amount changed. | ✓ accepts a suggestion, ✕ dismisses it. ✎ edits the name, category, amount and rhythm (monthly, quarterly, yearly). |
| **Transactions** | The month's transactions. A dashed category is automatic and not confirmed yet. | Search and filter; filtering on a category includes its sub-categories. Change a category: it applies to every payment of that payee, with an "Only this one" option. ✓ confirms an automatic category. ☆ marks the payee as a fixed payment. ✂ splits a spending payment into parts, each with its own spending category, amount and optional label (for example a supermarket receipt): the parts must add up to the payment, the first part takes what is left, and "Remove split" undoes it. A split payment shows its parts; totals, charts, the category filter and the export count each part in its own category. |
| **To review** | Every payee whose automatic category you have not confirmed. | Confirm one by one, or all shown at once. |
| **Categories** | Your categories as a tree per type (spending, income, savings, own transfers), with their totals per month. A total includes the sub-categories. | ▸ shows or hides the sub-categories. "+ sub-category" adds one below a category (up to four levels). ✎ edits the name, parent, colour and keywords; the type (spending, income, savings, own transfer) can be set on top-level categories only, a sub-category follows its top-level category. Deleting a category moves its payments and rules to its parent (to Other for a top-level one); built-in categories cannot be deleted. |
| **Settings** | The bank connection, the stored history and the data folder. | Reconnect the bank. Export every transaction as comma-separated values (CSV); a split payment gives one line per part. |
