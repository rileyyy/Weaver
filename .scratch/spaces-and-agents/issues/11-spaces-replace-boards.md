# 11: Spaces replace Boards

**What to build:** A **Space** (Home, Village, Software…) is a stored entity holding a forest of work-item trees. Every work item belongs to exactly one Space, and drill-down works inside it as it does now. The Board entity is removed, and the app lets you switch Space instead of reading the first Board. Admins create and rename Spaces on the Admin page. Reparenting never crosses a Space boundary. Statuses, priorities and Layers stay global in this ticket. See ADR-0001.

**Blocked by:** 10

**Status:** ready-for-agent

- [ ] Space entity with name; work items carry a required Space; a new root item is created in a chosen Space, and children inherit their parent's
- [ ] The Board entity, its endpoints, MCP tools and UI are removed (clean schema change, no data migration)
- [ ] Reparenting into a different Space is rejected with a domain error, alongside the existing cycle check
- [ ] Repeating-item copies are created in their template's Space
- [ ] The app shows the current Space and lets the user switch; the last-used Space is remembered locally
- [ ] Admins create and rename Spaces in the UI; agents can list Spaces over MCP but not change them
- [ ] Space creation and item Space are recorded in history
- [ ] Integration tests cover cross-Space reparent rejection
