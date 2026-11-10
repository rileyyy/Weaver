# 23: Saved Views, Overview, and a default View

**What to build:** A **View** is a saved, shared query: one or more Spaces, filters, and a layout (Board, Hierarchy or Roadmap; Agenda comes in ticket 24). Anyone can save the current header state as a View, open, rename or delete Views, and pick their own default View. **Overview** is built in: everything in a Doing-category status or dated in the window, across every Space, grouped by Space. A Space breadcrumb shows where you are. See ADR-0006.

**Blocked by:** 22

**Status:** done

- [x] Views are stored server-side, shared by all users, with name, Spaces, query inputs and layout
- [x] Each user has a default View, opened on launch; falls back to Overview
- [x] Overview can't be deleted or edited
- [x] Cross-Space layouts group by Space; the breadcrumb shows Space then drill-down path
- [x] Agents can list Views and run one over MCP
- [x] Integration tests for View storage; widget tests for save and switch

## Comments

**2026-11-10, on implementation (`feature/saved-views-backend`, then `feature/saved-views-flutter` stacked on it):**

- **Decided with Riley before building:**
  - A Board of several Spaces is a section per Space, each with its own status columns (statuses are per Space), not shared category columns.
  - A View stores no time range: saved dates go stale. The header's range applies to whichever View is open, and `run_view` takes `from`/`to`.
  - With no window, Overview's "dated in the window" is any dated item; a header range narrows only the dated items, never the Doing ones.
  - Shipped as two stacked PRs, backend then Flutter.
- **What a View stores:** Spaces (empty means every Space, including later ones), text, tags, tag match, done-hiding, Overview's rule (`scheduledOrInProgressOnly`) and a layout. Hidden status columns and sort aren't stored; they're layout, not query inputs.
- **Overview** is built in rather than stored, at a fixed id. Its name counts as taken, and changing or deleting it is a 400.
- **Default View** is a `DefaultViews` row per user, not a `User` column. Choosing Overview or deleting the View removes the row.
- **Launch** opens the default View, replacing the last-used-Space memory.
- **The breadcrumb** starts at the View when it spans several Spaces, else at its one Space. Drilling in from the View pushes the Space, then the item. The Hierarchy and Roadmap follow the Space the breadcrumb is in.
- **Not verified here:** the Postgres integration tests (`ViewTests`, the new `WorkItemSearchTests` case) couldn't run locally, because this machine has no Docker. CI runs them on the backend PR. Nobody ran the app by hand; the Flutter side is covered by unit and widget tests.
