# 12: Per-Space statuses and priorities

**What to build:** Each Space owns its own statuses and priorities, so Home can use Quoted / Booked / Done while Software uses New / Active / Resolved. Admins manage them per Space on the Admin page. A work item can only take a status or priority from its own Space. See ADR-0001.

**Blocked by:** 11

**Status:** done

- [x] Statuses and priorities belong to a Space; a new Space is seeded with a sensible default set
- [x] Setting a status or priority from another Space is rejected
- [x] The Admin page manages statuses and priorities per Space (create, rename, recolour, reorder, delete when unused), as it does globally today
- [x] Board columns and pickers show only the current Space's statuses and priorities
- [x] MCP lookup tools take or return the Space
- [x] The last-value and in-use protections still hold per Space

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Moving items off a deleted status already records a `statusId` history entry per item (`StatusService.MoveWorkItemsAsync`); keep that when statuses become per-Space.

**2026-11-03, on closing:**

- Kept: moving items off a deleted status still records a `statusId` history entry per item.
- REST takes the Space as a required `spaceId` query parameter (body for create) rather than nested routes, matching `/work-items` and swimlanes.
- The migration gives existing Spaces the defaults but doesn't remap work items: a database with work items must be reset, as for ticket 11.
- Not run here: the Postgres integration tests (no Docker on this machine). They compile, including the new `PerSpaceLookupTests`; run them before merging.
