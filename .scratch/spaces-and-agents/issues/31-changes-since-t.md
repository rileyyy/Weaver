# 31: Changes since T

**What to build:** An agent asks "what changed since yesterday?" and gets the work items changed after a timestamp, with who changed what, including deletes. It's built on the change history, over MCP and REST. See ADR-0005.

**Blocked by:** 10

**Status:** ready-for-agent

- [ ] Takes a timestamp and optional Space; returns changed items as lean summaries, each with its history entries since T
- [ ] Deleted and restored items are included, and marked
- [ ] Pages with a stable cursor
- [ ] Optionally excludes the caller's own changes, so an agent can see what others did
- [ ] Tests cover field changes, deletes, and paging

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- `WorkItemHistory` has an index on `CreatedAtUtc` for this. Order by `Id` (identity), not the timestamp: entries saved in one write share a timestamp, and `Id` makes a stable cursor. Entries by Weaver itself (background recurrence generation) have a null `AccountId`, so "exclude my own changes" must not drop them by accident. Deletes are already entries (`deleted`), and the history of a deleted item still reads.
