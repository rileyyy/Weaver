# 12: Per-Space statuses and priorities

**What to build:** Each Space owns its own statuses and priorities, so Home can use Quoted / Booked / Done while Software uses New / Active / Resolved. Admins manage them per Space on the Admin page. A work item can only take a status or priority from its own Space. See ADR-0001.

**Blocked by:** 11

**Status:** ready-for-agent

- [ ] Statuses and priorities belong to a Space; a new Space is seeded with a sensible default set
- [ ] Setting a status or priority from another Space is rejected
- [ ] The Admin page manages statuses and priorities per Space (create, rename, recolour, reorder, delete when unused), as it does globally today
- [ ] Board columns and pickers show only the current Space's statuses and priorities
- [ ] MCP lookup tools take or return the Space
- [ ] The last-value and in-use protections still hold per Space

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Moving items off a deleted status already records a `statusId` history entry per item (`StatusService.MoveWorkItemsAsync`); keep that when statuses become per-Space.
