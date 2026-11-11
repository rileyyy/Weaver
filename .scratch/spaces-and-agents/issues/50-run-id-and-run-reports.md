# 50: Run id on agent writes, and an admin run reports page

**What to build:** An agent can tag its writes with a **run id**, so everything one run did can be reviewed together. Admins get a reports page listing runs per agent: collapsed by default, showing the agent and the run's date, and expandable to show each change. See ADR-0010.

**Blocked by:** 10

**Status:** needs-triage

- [x] MCP write tools take an optional `runId` argument and REST writes accept an `X-Agent-Run` header, carried the same way as the actor label
- [x] History stores the run id in a new nullable column, and history DTOs return it
- [ ] An admin-only endpoint lists runs (agent account, key, run id, first and last change, number of changes), newest first, paged
- [ ] The Admin page has a Runs view: one collapsed row per run showing agent and date, expanding to the changes (item, field, old → new, actor label)
- [ ] The runner (ticket 46) generates one run id per run and sends it on every write
- [ ] Tests cover the run id reaching history over REST and MCP, and the run list's grouping and paging

## Decisions

Settled in a grilling session on 2026-11-11.

- A run id, rather than grouping by gaps in time (fuzzy) or putting the run in the actor label (overloads a field meant for sub-agents).
- A run that changed nothing, or crashed, doesn't appear. The runner's logs cover that.
