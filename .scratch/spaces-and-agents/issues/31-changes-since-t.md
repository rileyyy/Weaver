# 31: Changes since T

**What to build:** An agent asks "what changed since yesterday?" and gets the work items changed after a timestamp, with who changed what, including deletes. It's built on the change history, over MCP and REST. See ADR-0005.

**Blocked by:** 10

**Status:** done

- [x] Takes a timestamp and optional Space; returns changed items as lean summaries, each with its history entries since T
- [ ] Deleted and restored items are included, and marked
  - Deletes are done (`deleted: true`, title and Space from history). Restore doesn't exist until the Trash, so there's nothing to mark yet; ticket 19 has a comment on adding `restored` here.
- [x] Pages with a stable cursor
- [x] Optionally excludes the caller's own changes, so an agent can see what others did
- [x] Tests cover field changes, deletes, and paging
  - The Application tests (InMemory) pass. The Postgres integration tests (`WorkItemChangesTests`: paging, cursor, exclude-own with a system entry, non-UTC `since`, MCP with a read-only key) compile but weren't run on the machine that built this, which has no Docker. Run them before merging.

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- `WorkItemHistory` has an index on `CreatedAtUtc` for this. Order by `Id` (identity), not the timestamp: entries saved in one write share a timestamp, and `Id` makes a stable cursor. Entries by Weaver itself (background recurrence generation) have a null `AccountId`, so "exclude my own changes" must not drop them by accident. Deletes are already entries (`deleted`), and the history of a deleted item still reads.
