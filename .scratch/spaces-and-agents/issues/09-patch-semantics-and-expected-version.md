# 09: Patch semantics and `expectedVersion` on every write

**What to build:** Field updates over REST and MCP use patch semantics: a field left out is unchanged and `null` clears it, so an agent calling `update_work_item_details` with just a title no longer wipes the description, layer and priority. Every work-item write accepts an optional `expectedVersion` and returns 409 when it's stale, including status, parent and assignee, which are last-write-wins today. Writes return the new version and the fields that changed. Status and parent changes stay separate operations. Clean redesign over compatibility: Weaver isn't deployed.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Details, schedule and tags updates distinguish "absent" from `null` over REST and MCP
- [ ] Every work-item write accepts an optional `expectedVersion`; a stale one returns a conflict
- [ ] Write responses (REST and MCP) include the new version and the list of changed fields
- [ ] The Flutter detail dialog still works and sends `expectedVersion` for everything it edits
- [ ] Integration tests against real Postgres cover omit vs `null`, and stale versions on each write
