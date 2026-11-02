# 23: Saved Views, Overview, and a default View

**What to build:** A **View** is a saved, shared query: one or more Spaces, filters, and a layout (Board, Hierarchy or Roadmap; Agenda comes in ticket 24). Anyone can save the current header state as a View, open, rename or delete Views, and pick their own default View. **Overview** is built in: everything in a Doing-category status or dated in the window, across every Space, grouped by Space. A Space breadcrumb shows where you are. See ADR-0006.

**Blocked by:** 22

**Status:** ready-for-agent

- [ ] Views are stored server-side, shared by all users, with name, Spaces, query inputs and layout
- [ ] Each user has a default View, opened on launch; falls back to Overview
- [ ] Overview can't be deleted or edited
- [ ] Cross-Space layouts group by Space; the breadcrumb shows Space then drill-down path
- [ ] Agents can list Views and run one over MCP
- [ ] Integration tests for View storage; widget tests for save and switch
