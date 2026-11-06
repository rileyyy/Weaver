# 16: Move a work item between Spaces

**What to build:** A user or read-write agent moves a work item, with its whole subtree, into another Space as one explicit operation. It maps each item's status, priority, Type and custom fields onto the target Space's configuration, showing the mapping before it's confirmed. See ADR-0001.

**Blocked by:** 14

**Status:** done

- [x] The move takes a target Space and optional target parent there, plus a status and Type mapping; unmapped values fall back to stated defaults
- [x] Custom-field values without a matching field on the target Type follow the same rule as changing Type (ticket 14)
- [x] The whole subtree moves atomically; links (which may cross Spaces) are kept
- [x] Recorded in history for every moved item
- [x] Available in the detail dialog and over MCP
- [x] Ordinary reparenting still can't cross Spaces

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Moving an item between Spaces must record history for the item (and every descendant it moves), attributed to the caller.

**2026-11-04, from ticket 14 (custom fields on Types):**

- A move changes the item's Type (Types are per Space), so go through `WorkItem.ChangeType` or `UpdateDetails`: they clear the item's custom field values and return a `fields.<fieldId>` change per cleared value for history. Don't set `TypeId` around them.

**2026-11-06, on closing:**

- Defaults: a status maps to the target status with the same name (ignoring case), else the target's first status in the same category, else its first status. A Type or priority maps to its namesake, else it's cleared. The plan (`space-move-plan`, `plan_work_item_space_move`) shows them; the dialog sends every mapping it showed, so a later change to the defaults can't change a confirmed move.
- Custom fields: the Type changes through `WorkItem.ChangeType`, so values are cleared and each is recorded, as ticket 14 decided. Nothing carries over to a same-named field.
- Sub-items in the Trash move with the subtree; otherwise restoring one would put it under a parent in another Space. The plan counts them (`trashedItemCount`).
- Atomic: one save. Not covered: a sub-item created or reparented under a moving item between the move's read and its save stays in the old Space. Closing that needs a database rule, so it's ticket 48.
- Not run here: `SpaceMoveTests` in the Postgres integration tests (recursive walk with the Trash, REST and MCP key scopes, history, links), for lack of Docker. CI runs them; check before merging. The dialog was not tried by hand in a running app.
