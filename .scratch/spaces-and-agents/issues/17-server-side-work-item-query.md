# 17: Server-side work-item query, with MCP search

**What to build:** One Application-layer query serves every client. Its inputs are Space(s), free text, `#Number`, status category, assignee, tags (OR or AND; tags are global across Spaces), Type, date window, Backlog-only and updated-since. It pages with a stable cursor, not an offset over the mutable rank. It's exposed over REST and as an MCP search tool, so agents can finally find items and look up `#Number`. The Flutter switch-over is ticket 18. See ADR-0006 and, for the date-window rule, ADR-0003.

**Blocked by:** 13

**Status:** done

- [x] Every input above is supported, and combinations AND together
- [x] The date window uses the new rule: undated items are excluded, and an undated parent is included when a descendant matches
- [x] Backlog-only returns non-Done items with no dates
- [x] Cursor paging neither skips nor repeats items when ranks change between pages, with a test
- [x] Text search uses Postgres full-text search with supporting indexes
- [x] REST endpoint and MCP search tool; `#123` resolves directly
- [ ] Integration tests against real Postgres for each filter
  - `WorkItemSearchTests` has a test for each filter, plus tests for:
    - the AND combination
    - the descendant rule
    - cursor stability when ranks change between pages
    - bad arguments
    - MCP with a read-only key
    - an `EXPLAIN` showing the text search can use its GIN index
  - The tests compile but haven't run: the machine that built this has no Docker. CI runs them, so check that run before merging.
  - The InMemory Application tests (`WorkItemQueryServiceTests`) pass, and a unit test confirms the search's SQL uses the index's exact `to_tsvector` expression.

## Comments

**2026-11-04, from the implementation:**

- **Undated parents:** a parent with no dates is included only when a descendant matches the whole query, not just the date window, so a layout never draws a parent with nothing listed under it.
- **Include-deleted:** ADR-0006 lists this input, but it isn't here because nothing is soft-deleted until the Trash. Ticket 19 should add it.
- **Done cutoff:** excluding Done items completed before a cutoff belongs to ticket 21.
- **Text search and tags:** text search covers title, description and When, but not tags. The old client-side search also matched tags. Ticket 18 should send typed tags through the tag filter, or a later ticket can add tags to the index.
- **Result shapes:** REST `GET /api/work-items/search` returns full items, for ticket 18. MCP `search_work_items` returns `WorkItemSummary`s, ready for ticket 29's null-omission.
