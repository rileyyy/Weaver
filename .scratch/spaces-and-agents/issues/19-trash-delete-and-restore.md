# 19: Trash: delete and restore

**What to build:** Deleting a work item moves it and its subtree to the **Trash** instead of destroying it. Every normal read excludes trashed items; the query's include-deleted flag is the only way to see them. Humans and read-write agents can view the Trash and restore from it, in the app and over MCP. Purging and retention are ticket 20. See ADR-0007.

**Blocked by:** 10, 17

**Status:** done

- [x] Delete trashes the item and its subtree; links, comments and attachments are kept
- [x] All reads (lists, query, roll-ups, counts, link targets) exclude trashed items unless include-deleted is set
- [x] Restore puts the item back under its nearest ancestor that still exists, or as a root of its Space
- [x] Repeating items: trashing or restoring a generated copy doesn't make the generator recreate or duplicate it, and trashing a template stops its schedule; decide the rules and test them
- [x] Delete and restore are recorded in history
- [x] A Trash page lists trashed items with who deleted them and when, with restore
- [x] MCP can list and restore Trash; read-only keys can list only

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Delete records a `deleted` entry (old value: the title) for the item and every descendant, plus a `link` removal on items outside the subtree that lose a link. History has no foreign key to `WorkItems`, so it already outlives a hard delete. Add a `restored` name to `WorkItemHistoryField` (and the Flutter `change` text) for restore.
- An account with history can't be hard-deleted (`UserHasHistoryException`), so "who deleted it" on the Trash page can join `WorkItemHistory` to `Users` safely.

**2026-11-03, from ticket 31 (changes since T):**

- `WorkItemChangesService` (`GET /api/work-items/changes`, MCP `list_changes_since`) marks an item `Deleted` when it has no `WorkItems` row, and rebuilds a deleted item's summary and Space from history (`DeletedItemTrace`). Once delete trashes instead, a trashed item keeps its row, so:
  - Derive `Deleted` from the trash state (the row's deleted marker), not from the row being missing, and build a trashed item's summary from its row (number and all) instead of from `DeletedItemTrace`. Purged items (ticket 20) still go through `DeletedItemTrace`.
  - The live-summary query must include trashed rows here, even after the global "exclude trashed" read rule lands, or trashed items lose their summary.
  - Add `Restored` to `ChangedWorkItem` (and `ChangedWorkItemDto`): true when the item has a `restored` entry among its changes since T. That's the ticket 31 criterion "Deleted and restored items are included, and marked" left unticked. If both flags together read badly (deleted, then restored, then deleted again), consider one state derived from the latest `deleted`/`restored` entry instead.
  - The Space filter treats a trashed item like a live one (its row's Space); test it alongside the restore tests.

**2026-11-04, implementation (`feature/trash-delete-and-restore`):**

- Rules decided here:
  - **Restore works per delete.** `TrashRootId` records which delete trashed each item. Restore only accepts the item a delete was made on, and brings back exactly the items that went with it, so a child deleted separately earlier stays in the Trash. Restoring an item that went with its ancestor is a 400 that names the ancestor. The Trash page lists one row per delete.
  - **Repeating items.** A trashed copy still counts as generated, so it is never recreated or duplicated. Restoring it brings back the same row. Trashing a template hides its recurrence, so generation skips it. Restoring the template restarts generation from today, with no backfill for the time it spent in the Trash. Copies generated before the template was trashed stay live.
  - **Links aren't recorded as lost.** When an item is trashed, a surviving item's link to it is hidden, not removed, so the survivor gets no `link` history entry. The trashed item's own `deleted` entry is the record. Purge (ticket 20) is when the link is really removed and should record it.
  - **Changes since T.** `Deleted` and the new `Restored` describe where the item ended up, so at most one is set.
  - The Flutter Trash tab lists every Space, like the Repeating tab, and shows each row's Space.
- MCP: `delete_work_item` and `list_trash` (read-only), plus `restore_work_item`. That name is not `restore_from_trash`, because `McpTests` requires agent write tools to be named for work items.
- **Not run:** the Postgres integration tests added here (`TrashTests`, plus additions to `HierarchyQueryTests`, `McpTests` and `WorkItemPersistenceTests`). The machine that built this has no Docker. Every criterion is covered by the InMemory service tests and the Flutter tests, but the raw-SQL descendant walk and the FK and unique-index behaviour are only verified once `dotnet test tests/Weaver.Api.IntegrationTests` passes.
