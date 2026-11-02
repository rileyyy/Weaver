# 1. Spaces own configuration; Views replace Boards

Status: accepted (2026-11-02)

## Context

Today every work item sits in one global tree, and statuses, layers and
priorities are global too. The Board entity holds just a name and a scope
item, and the app reads only the first Board. Weaver has to serve very
different kinds of work: a renovation wants statuses like Quoted / Booked,
while software wants New / Active / Resolved. Its two human users also care
about different areas.

## Decision

- **A Space is a stored entity.** Examples: Home, Village, Software. It holds
  a forest of work-item trees of any depth, and drill-down works inside it
  as it does now. Every work item belongs to exactly one Space.
- **Configuration lives on the Space only, not on nodes inside it.** Each
  Space owns its statuses, priorities, Types and done-hiding period. Tags
  aren't Space configuration: they stay global, so one tag can be used and
  filtered on across Spaces.
- **Reparenting never crosses a Space boundary.** Moving an item to another
  Space is a separate, explicit operation that maps its status, Type and
  custom fields onto the target Space's configuration.
- **The Board entity is removed.** A **View** is a saved, shared query over
  one or more Spaces. The Board, Hierarchy, Roadmap and Agenda are layouts a
  View can use (see [ADR-0006](0006-server-side-query-and-views.md)).
  **Overview** is a built-in cross-Space View.
- **Links may cross Spaces** (see
  [ADR-0009](0009-typed-links-and-dependency-flags.md)).

We rejected configuration inherited at any node (nearest ancestor wins).
Every reparent would become a possible remapping, and "which statuses apply
here?" would stop having one answer.

## Consequences

- Lookups gain a Space id, and every service call that resolves a status or
  Type must be scoped by Space.
- The "is this its own ancestor" check and reparenting must also reject a
  move into another Space.
- Weaver isn't deployed yet, so the schema can change without migrating
  data.
