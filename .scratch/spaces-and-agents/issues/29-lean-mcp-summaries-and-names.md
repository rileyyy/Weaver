# 29: Lean MCP summaries, and names in DTOs

**What to build:** MCP list tools return lean summaries: Number, title, Type and status names, dates, assignee name and child count, with null fields omitted, so an agent can read a list without a lookup per id. Detail and REST responses carry names alongside ids. Full detail comes from `get_work_item`.

**Blocked by:** 17

**Status:** done

- [x] Every MCP list tool returns the summary shape; nulls are omitted from MCP JSON
  - `list_repeating_work_items` and `list_work_item_links` keep their own short item references (number and title), which never were full items.
- [x] `get_work_item` (by id or `#Number`) returns the full item with names
  - It takes an `id` or a `number` (an int, like `search_work_items`' `number`), exactly one of them.
- [x] Summary loading doesn't introduce N+1 queries, with a test or query count check
  - `SummaryQueryCountTests` counts SQL on Postgres: children and all-items summaries are one query, search is two, and the REST name lookup is at most six, for 1 or 6 children alike.
- [x] MCP tests assert the summary shape
  - The Postgres integration tests were written without Docker on the implementing machine, so CI is their first run.

## Comments

**2026-11-03, from ticket 31 (changes since T):**

- `WorkItemSummary` (Application) / `WorkItemSummaryDto` (Api) already exist for `list_changes_since`: id, number, title, Space id and name, status name, layer name, dates, assignee name and child count, loaded in one projection. Reuse and extend them (e.g. layer → Type in ticket 13) rather than building a second summary type. Number and the rest are nullable because a deleted item's summary only has its title and Space.
- `list_changes_since` still sends nulls, and each change is a full `WorkItemHistoryEntryDto`. The MCP null-omission this ticket adds should cover both.

**2026-11-05, from the implementation:**

- **History keeps its nulls.** The comment above asked for null omission to cover `list_changes_since` too. Its summaries do, but a history entry's `oldValue`/`newValue` are always written (`[JsonIgnore(Condition = Never)]`): there a null is the fact recorded (the field was empty, or was cleared), and an absent property would leave the reader guessing. See `docs/architecture.md`.
- **Write results don't show a cleared field.** With nulls omitted, a field cleared by an MCP write is simply absent from the returned item, and `changedFields` names it. That's enough to tell a clear from no change, but not to see the old value; use the history for that.
- **`parentId` on summaries.** Added so `list_all_work_items` can still be rebuilt into a tree. It also appears on the REST trash, changes and search responses.
- **Names differ between shapes.** The detail item says `assignedToUsername` (next to `assignedToUserId`) where the summary says `assigneeName`, and only the detail item has `parentNumber`/`parentTitle`. Ticket 32's MCP reference should explain both, or a later ticket can align them.
- **Flutter can drop lookups.** The client ignores the new name fields today. It could stop resolving assignee, status, Type and priority names and the parent's Number and title itself, though it still needs the user list for the assignee picker.
