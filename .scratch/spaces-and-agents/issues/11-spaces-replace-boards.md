# 11: Spaces replace Boards

**What to build:** A **Space** (Home, Village, Software…) is a stored entity holding a forest of work-item trees. Every work item belongs to exactly one Space, and drill-down works inside it as it does now. The Board entity is removed, and the app lets you switch Space instead of reading the first Board. Admins create and rename Spaces on the Admin page. Reparenting never crosses a Space boundary. Statuses, priorities and Layers stay global in this ticket. See ADR-0001.

**Blocked by:** 10

**Status:** done

- [x] Space entity with name; work items carry a required Space; a new root item is created in a chosen Space, and children inherit their parent's
- [x] The Board entity, its endpoints, MCP tools and UI are removed (clean schema change, no data migration)
- [x] Reparenting into a different Space is rejected with a domain error, alongside the existing cycle check
- [x] Repeating-item copies are created in their template's Space
- [x] The app shows the current Space and lets the user switch; the last-used Space is remembered locally
- [x] Admins create and rename Spaces in the UI; agents can list Spaces over MCP but not change them
- [x] Space creation and item Space are recorded in history
- [ ] Integration tests cover cross-Space reparent rejection

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Anything that changes a work item must record history (`WorkItemHistoryRecorder`, see "Change history" in `docs/architecture.md`). A Space on a work item is a field like any other: return `FieldChange`s from the domain method and record them, and add the id field to `HistoryValueNames` so the History tab shows the Space's name, not its id. Board create/delete records nothing today (boards aren't work items).

**2026-11-03, implementation (`feature/spaces-replace-boards`):**

- The cross-Space reparent integration tests (`SpaceTests` in `Weaver.Api.IntegrationTests`, REST and MCP) are written and compile, but haven't run: the machine they were written on has no Docker for Testcontainers. Tick the last box once they pass. Unit tests cover the same rule in the domain and the service.
- Space creation and renames go to a separate `SpaceHistory` table, since work item history is keyed by a work item. Nothing reads it yet (no endpoint or UI): ticket 47. An item's own history records its Space on create.
- A child created with a `spaceId` other than its parent's is a 400, not a move.
- The migration needs an empty `WorkItems` table (no data migration, as asked); reset local databases.

**2026-11-03, code review:**

- Fixed: switching Space could leave the old Space's items in the Hierarchy and Roadmap (until the reload returned, for good if it failed, or when a first load was still in flight); moving items off a deleted status ranked top-level items as one cell across Spaces; Space history used different JSON options from work item history; the Admin page sorted Spaces case-sensitively.
- Left as is: the domain re-checks the reparent rules in `MoveToParent` after the service's early `EnsureCanMoveUnder` (deliberate, so every caller is covered); `GetChildrenAsync`'s "parent or Space" pair of nullable ids, and `CreateAsync`'s long optional parameter list, are worth a request type when ticket 17's server-side query reshapes these calls; `SpaceAdminRepository.list` and `SpaceRepository.loadSpaces` share an endpoint but keep the admin/read split the statuses already have.
