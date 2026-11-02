# 26: Typed links, URL links, and the link picker

**What to build:** Links between work items get a type: *related*, *blocks / blocked by*, or *duplicates*, and they can cross Spaces. A **URL link** points outside Weaver with an optional label (a PR, a quote, a product page). In the detail dialog, links are added through a search picker backed by the server query, not by raw id. See ADR-0009.

**Blocked by:** 10, 17

**Status:** ready-for-agent

- [ ] Link type is required; blocks is stored once and shown from each side as blocks or blocked by
- [ ] Duplicate and self links are rejected as today; a blocks cycle between two items is rejected
- [ ] URL links have a URL (validated) and optional label
- [ ] The picker searches by title and `#Number` across Spaces
- [ ] Link changes are recorded in history on both items
- [ ] MCP can add, list and remove both kinds
