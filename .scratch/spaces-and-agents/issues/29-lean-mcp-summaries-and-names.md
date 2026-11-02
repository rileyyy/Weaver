# 29: Lean MCP summaries, and names in DTOs

**What to build:** MCP list tools return lean summaries: Number, title, Type and status names, dates, assignee name and child count, with null fields omitted, so an agent can read a list without a lookup per id. Detail and REST responses carry names alongside ids. Full detail comes from `get_work_item`.

**Blocked by:** 17

**Status:** ready-for-agent

- [ ] Every MCP list tool returns the summary shape; nulls are omitted from MCP JSON
- [ ] `get_work_item` (by id or `#Number`) returns the full item with names
- [ ] Summary loading doesn't introduce N+1 queries, with a test or query count check
- [ ] MCP tests assert the summary shape
