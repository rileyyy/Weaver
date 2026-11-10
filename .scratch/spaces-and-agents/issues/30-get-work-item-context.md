# 30: `get_work_item_context` with `include`

**What to build:** One MCP call gives an agent the context around a work item. It takes an `include` list (ancestors, children, comments, links, history, and later attachments) and returns only what was asked for, so an agent can orient itself in one round trip.

**Blocked by:** 10, 26, 29

**Status:** done

- [x] Accepts an item by id or `#Number` and an `include` list; unknown include values are an error listing the valid ones
  - `get_work_item_context` takes `id` or `number` like `get_work_item` (shared through `WorkItemReference`). It always returns the full item; `links` also brings the item's URL links, as `urlLinks`.
- [x] Children and ancestors use lean summaries; history is newest first and limited, with a way to ask for more
  - Ancestors run from the top-level item down to the parent. History pages with `historyOffset`/`historyLimit` (20 by default, at most 200) and returns `nextHistoryOffset` while more exists, through the new `IWorkItemHistoryService.ListPageForWorkItemAsync`.
- [x] Trashed related items are excluded
  - Children, ancestors and links go through the Trash filter. A live item under a trashed ancestor can't normally happen (restore re-homes it), so the ancestor case is tested only on InMemory, not on Postgres.
- [x] MCP tests for each include
  - `WorkItemContextToolsTests` has one per include. `McpTests.Mcp_GetWorkItemContext_ByNumber_ReturnsOnlyTheIncludes_AndLeavesTheTrashOut` covers every include except attachments end to end on Postgres. It was written without Docker on the implementing machine, so CI is its first run.

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- `IWorkItemHistoryService.ListForWorkItemAsync` already returns an item's history newest first with account name and kind, key label, actor label, raw JSON values and names for id values; the `history` include can reuse it (and `WorkItemHistoryEntryDto`).


**2026-11-05, from ticket 38 (attachments):**

- Ticket 38 asks that `get_work_item_context` can include attachments. `IWorkItemAttachmentService.ListForWorkItemAsync` returns the metadata (`AttachmentDto` over MCP); include metadata only and let the agent fetch content with `get_work_item_attachment`, which caps a fetch at 5 MiB. Tick ticket 38's MCP criterion once it's in.
