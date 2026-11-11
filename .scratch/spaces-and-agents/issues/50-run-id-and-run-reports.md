# 50: Run id on agent writes, and an admin run reports page

**What to build:** An agent can tag its writes with a **run id**, so everything one run did can be reviewed together. Admins get a reports page listing runs per agent: collapsed by default, showing the agent and the run's date, and expandable to show each change. See ADR-0010.

**Blocked by:** 10

**Status:** done

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
- **One run's changes aren't paged.** A run is expected to touch tens of items; page them if real runs get large. There is no cap: a very large run returns every change in one response, so revisit this if runs grow.
- **A run is an agent's.** Only agents sign in with an API key, so the run list and a run's changes only count entries with a key. A human's REST write that sends `X-Agent-Run` still records the run id in history, but is never listed as a run. The key is therefore always part of a run, and `apiKeyId` is required on `GET /api/admin/runs/changes`.
- **Space history has no run id.** Space writes are admin-only and agents can't be admins, so no agent ever writes one and `SpaceHistoryEntry` doesn't need the column.
- **`actorLabel` and `runId` stay separate, flat arguments on every MCP write tool**, since MCP arguments must stay flat. A future attribution field would have to be added in the same places.

## Comments

**Review (2026-11-11).** A code review asked for these changes, all made on this branch:

- `_ItemChanges` moved out of `run_changes_list.dart` into its own `RunItemChanges` widget (one class per file).
- Runs are agent runs only: the list and a run's changes skip entries without an API key, `RunKey`/`RunReport` and the DTO now require the key, and `apiKeyId` is required on the changes endpoint.
- `RunNotFoundException` names the key as well as the account and run id.
- The frontend names a run with a `RunKey` value (with `==`/`hashCode`) instead of a `'|'`-joined string.
- The frontend no longer copies the server's page size: it sends only an offset and follows the `nextOffset` the server returns.
- The offset/limit checks shared by run reports, item history and the item list are one `PagingGuard`.
- `AdminListFrame` takes one optional `AdminAddAction` (label and handler together) instead of separate `addLabel`/`onAdd`.
- Recorded why Space history has no run id, that one run's changes are uncapped, and that a future attribution field would touch every MCP write tool's arguments (see Decisions).
