# 17: Server-side work-item query, with MCP search

**What to build:** One Application-layer query serves every client. Its inputs are Space(s), free text, `#Number`, status category, assignee, tags (OR or AND; tags are global across Spaces), Type, date window, Backlog-only and updated-since. It pages with a stable cursor, not an offset over the mutable rank. It's exposed over REST and as an MCP search tool, so agents can finally find items and look up `#Number`. The Flutter switch-over is ticket 18. See ADR-0006 and, for the date-window rule, ADR-0003.

**Blocked by:** 13

**Status:** ready-for-agent

- [ ] Every input above is supported, and combinations AND together
- [ ] The date window uses the new rule: undated items are excluded, and an undated parent is included when a descendant matches
- [ ] Backlog-only returns non-Done items with no dates
- [ ] Cursor paging neither skips nor repeats items when ranks change between pages, with a test
- [ ] Text search uses Postgres full-text search with supporting indexes
- [ ] REST endpoint and MCP search tool; `#123` resolves directly
- [ ] Integration tests against real Postgres for each filter
