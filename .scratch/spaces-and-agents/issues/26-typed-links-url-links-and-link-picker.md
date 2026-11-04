# 26: Typed links, URL links, and the link picker

**What to build:** Links between work items get a type: *related*, *blocks / blocked by*, or *duplicates*, and they can cross Spaces. A **URL link** points outside Weaver with an optional label (a PR, a quote, a product page). In the detail dialog, links are added through a search picker backed by the server query, not by raw id. See ADR-0009.

**Blocked by:** 10, 17

**Status:** done

- [x] Link type is required; blocks is stored once and shown from each side as blocks or blocked by
- [x] Duplicate and self links are rejected as today; a blocks cycle between two items is rejected
- [x] URL links have a URL (validated) and optional label
- [x] The picker searches by title and `#Number` across Spaces
- [x] Link changes are recorded in history on both items
- [x] MCP can add, list and remove both kinds

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Link history records `link` with the other item's id as the value, on both items, and `HistoryValueNames` names it "#N title". Typed links need the type in the value (e.g. `{ "workItemId", "type" }`) and URL links the URL; update `HistoryValueNames.IdIn` and the Flutter `change` text when the value stops being a plain id.

**2026-11-03, from ticket 25 (When field and quick picks):**

- `WorkItem.IsWhenResolved` (sent as `whenResolved`) only counts dates today, since links aren't typed yet. When *blocked by* exists, make a blocked-by link count too (ADR-0003). It needs the item's links, so it can't stay a property computed from the row alone: compute it where the item is loaded with its links, or store a flag the link writes keep up to date.

**2026-11-04, on closing:**

- Duplicates is directional like blocks (duplicates / duplicated by). A pair of items still has at most one link of any type; a blocks link against one running the other way is rejected as a cycle (409). Only two-item cycles are checked. A self link stays 400 as before.
- `whenResolved` counts a live blocked-by link. It's computed per response (`WorkItemDtoMapper` + `IWorkItemBlockers`, one batched query), not stored, so a link write never bumps the blocked item's version. Trashing a blocker therefore changes `whenResolved` without a history entry, so changes-since-T won't flag it.
- URL links cascade with their item, so purge (ticket 20) drops them with no extra code; typed links stay Restrict.
- The new REST/MCP integration tests (`WorkItemLinkTests`) and the migration weren't run on this machine (no Docker); CI should cover them.
- For ticket 27: `IWorkItemBlockers` and `WorkItemLink.RelationFrom` are the seams for finding blockers.
