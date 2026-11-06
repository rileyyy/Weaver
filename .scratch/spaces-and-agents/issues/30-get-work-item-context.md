# 30: `get_work_item_context` with `include`

**What to build:** One MCP call gives an agent the context around a work item. It takes an `include` list (ancestors, children, comments, links, history, and later attachments) and returns only what was asked for, so an agent can orient itself in one round trip.

**Blocked by:** 10, 26, 29

**Status:** ready-for-agent

- [ ] Accepts an item by id or `#Number` and an `include` list; unknown include values are an error listing the valid ones
- [ ] Children and ancestors use lean summaries; history is newest first and limited, with a way to ask for more
- [ ] Trashed related items are excluded
- [ ] MCP tests for each include

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- `IWorkItemHistoryService.ListForWorkItemAsync` already returns an item's history newest first with account name and kind, key label, actor label, raw JSON values and names for id values; the `history` include can reuse it (and `WorkItemHistoryEntryDto`).


**2026-11-05, from ticket 38 (attachments):**

- Ticket 38 asks that `get_work_item_context` can include attachments. `IWorkItemAttachmentService.ListForWorkItemAsync` returns the metadata (`AttachmentDto` over MCP); include metadata only and let the agent fetch content with `get_work_item_attachment`, which caps a fetch at 5 MiB. Tick ticket 38's MCP criterion once it's in.
