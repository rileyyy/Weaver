# 33: Detail dialog: editable status and parent, and "add sub-item"

**What to build:** From a work item's detail dialog you can change its status (from its Space's statuses), change its parent through a search picker, and add a sub-item without leaving the dialog.

**Blocked by:** 12, 17

**Status:** ready-for-agent

- [ ] Status picker shows the item's Space's statuses and uses the status operation with `expectedVersion`
- [ ] Parent picker searches within the item's Space and rejects cycles with a clear message; "no parent" is allowed
- [ ] "Add sub-item" creates a child in the same Space and shows it in the dialog's children
- [ ] The board refreshes after closing, as it does for other edits
- [ ] Widget tests for each
