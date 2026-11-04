# 33: Detail dialog: editable status and parent, and "add sub-item"

**What to build:** From a work item's detail dialog you can change its status (from its Space's statuses), change its parent through a search picker, and add a sub-item without leaving the dialog.

**Blocked by:** 12, 17

**Status:** done

- [x] Status picker shows the item's Space's statuses and uses the status operation with `expectedVersion`
- [x] Parent picker searches within the item's Space and rejects cycles with a clear message; "no parent" is allowed
- [x] "Add sub-item" creates a child in the same Space and shows it in the dialog's children
- [x] The board refreshes after closing, as it does for other edits
- [x] Widget tests for each

## Comments

**2026-11-04, closing (branch `feature/detail-dialog-status-parent-sub-items`):**

- Criteria are covered by view-model and widget tests; nobody has run the app by hand, so the cycle message, the board refresh on close and the picker's feel are unseen against a real server.
- The server answers a reparent cycle with 409, like a stale version. `WorkItemStore` now reloads after a 409 and, if the version hasn't moved, treats it as a rule violation rather than a conflict. Board drag-to-reparent shares this path.
- The picker (`frontend/lib/shared/work_item_picker/`) takes a search function and an optional "none" label, so ticket 26's link picker can reuse it. It hides the item and its direct children; deeper descendants are only caught by the server's cycle check.
- "Add sub-item" creates a title-only child in the Space's first status, at the end of the list.
- Code review was skipped at the user's request; review the store change on the PR.
