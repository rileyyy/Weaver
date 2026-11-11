# 50: Run id on agent writes, and an admin run reports page

**What to build:** An agent can tag its writes with a **run id**, so everything one run did can be reviewed together. Admins get a reports page listing runs per agent: collapsed by default, showing the agent and the run's date, and expandable to show each change. See ADR-0010.

**Blocked by:** 10

**Status:** needs-triage

- [x] MCP write tools take an optional `runId` argument and REST writes accept an `X-Agent-Run` header, carried the same way as the actor label
- [x] History stores the run id in a new nullable column, and history DTOs return it
- [x] An admin-only endpoint lists runs (agent account, key, run id, first and last change, number of changes), newest first, paged
- [x] The Admin page has a Runs view: one collapsed row per run showing agent and date, expanding to the changes (item, field, old → new, actor label)
- [ ] The runner (ticket 46) generates one run id per run and sends it on every write
  - Not done here: the runner is being built in parallel under ticket 46. Whichever of 46 and 50 merges second sends `X-Agent-Run` (one fresh GUID per run) on every REST write the runner makes.
- [x] Tests cover the run id reaching history over REST and MCP, and the run list's grouping and paging
  - The Application tests (InMemory) and API unit tests pass. The Postgres integration tests (`WorkItemHistoryTests` run id cases, `RunReportTests`) compile but weren't run on the machine that built this, which has no Docker. Run them before merging.

## Decisions

Settled in a grilling session on 2026-11-11.

- A run id, rather than grouping by gaps in time (fuzzy) or putting the run in the actor label (overloads a field meant for sub-agents).
- A run that changed nothing, or crashed, doesn't appear. The runner's logs cover that.

Made while building it, on 2026-11-11:

- **The run id is free text**, trimmed, blank meaning none, at most 100 characters: the same rules as the actor label. The runner will send a GUID, but a hand-driven MCP client may prefer a readable name, and Weaver never parses it. A too-long one is a 400 and the write records nothing.
- **A run is its account, key and run id together**, since two agents could send the same free-text id. `GET /api/admin/runs/changes` takes all three as query parameters (the run id isn't in the path because it's free text).
- **An MCP tool's `runId` argument falls back to the `X-Agent-Run` header**, as `actorLabel` does, so an MCP client may also set it for a whole connection.
- **Runs are ordered by their last entry's id**, newest first, and paged by offset like item history.
- **One run's changes aren't paged.** A run is expected to touch tens of items; page them if real runs get large.
