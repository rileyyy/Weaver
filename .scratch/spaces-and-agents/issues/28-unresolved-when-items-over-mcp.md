# 28: Unresolved When items over MCP

**What to build:** Agents, whether Claude or a local model, can list the work items whose When is unresolved and resolve them by setting dates and/or adding a blocked-by link (e.g. "after drywall" links to the drywall item). This is how When phrases get turned into dates without Weaver containing an LLM. See ADR-0003.

**Blocked by:** 25, 26

**Status:** done

- [x] An MCP tool lists unresolved When items with their phrase, Space and parent, as lean summaries
  - `list_unresolved_when_items`. Summaries gained `when`. They already had the Space id and name and `parentId`, so there's no parent title; `get_work_item` gives that.
- [x] Resolving uses the existing schedule and link writes; the item then counts as resolved
  - `reschedule_work_item`, or `create_work_item_link` with `BlockedBy`. No new write.
- [x] The tool's description tells agents to search for referenced items before linking, and to leave ambiguous phrases for a human
- [x] Tests cover listing, and that resolving removes the item from the list
  - Service tests (InMemory) and MCP unit tests pass. The Postgres MCP test `Mcp_ListUnresolvedWhenItems_ListsThem_UntilTheScheduleOrABlockedByLinkResolvesThem` wasn't run on the implementing machine (no Docker), so CI is its first run.

## Comments

**2026-11-03, from ticket 25 (When field and quick picks):**

- "Unresolved" is `When is not null` and no start or end date (`WorkItem.IsWhenResolved`, `whenResolved` on the DTO). Filter on the same rule server-side rather than re-deriving it, and extend it with blocked-by links per ticket 26's comment.
- Agents set the phrase with `update_work_item_details` (`when`) and resolve it with `reschedule_work_item`; `GET /quick-picks` has no MCP tool yet, if agents need the server's idea of "next week".

**2026-11-10, on closing:**

- The list uses the item's own rule. `WorkItem.WhenResolutionNeedsBlockersRule` is the same predicate as `WhenResolutionNeedsBlockers`, and `BlockedWorkItems.BlockedIds` is the same blocked-ids query `WorkItemBlockers` uses. A test checks that the list agrees with `IsWhenResolved`.
- A blocker in the Trash doesn't count, even when the query reads trashed rows. `BlockedIds` checks the blocker's Trash state itself, because `IncludeDeleted` lifts the links' Trash filter from the whole query.
- **Done items are listed** when their When is unresolved, as the rule says. Agents can leave them out with `statusCategories`. Decide whether the default should exclude Done the way the Backlog does.
- A separate tool rather than an `unresolvedWhenOnly` flag on `search_work_items`, because the tool's description is where the agent guidance lives. The query has the flag, so `search_work_items` could expose it too if that's wanted.
- For ticket 32: the server instructions should point agents at `list_unresolved_when_items` for the When workflow.
