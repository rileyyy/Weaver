# 31: Changes since T

**What to build:** An agent asks "what changed since yesterday?" and gets the work items changed after a timestamp, with who changed what, including deletes. It's built on the change history, over MCP and REST. See ADR-0005.

**Blocked by:** 10

**Status:** ready-for-agent

- [ ] Takes a timestamp and optional Space; returns changed items as lean summaries, each with its history entries since T
- [ ] Deleted and restored items are included, and marked
- [ ] Pages with a stable cursor
- [ ] Optionally excludes the caller's own changes, so an agent can see what others did
- [ ] Tests cover field changes, deletes, and paging
