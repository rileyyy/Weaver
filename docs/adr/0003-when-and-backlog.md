# 3. A free-text When and a Backlog instead of sprints

Status: accepted (2026-11-02)

## Context

Most household and village work is "to do, but not today". Azure DevOps
handles this with sprints, which don't suit people who aren't developers.
And under today's time-filter rule, an item with no dates counts as
open-ended, so every someday item shows up in every time frame and clutters
"now".

## Decision

- **Every work item has an optional free-text When.** Examples: "before the
  Christmas market", "after drywall", "next spring".
- **Resolving a When gives it concrete form.** That means dates, a
  *blocked-by* link, or both. The item keeps the phrase alongside what it
  was resolved to.
- **The UI offers quick picks.** This weekend, next week and next month are
  resolved to dates in code. Dates and the When text can still be typed.
- **Weaver contains no LLM.** Agents, local or Claude, call MCP to list the
  unresolved When items and propose or set dates and links.
- **The Backlog holds every non-Done item with no dates.** Board and Roadmap
  show only items whose dates overlap the window.
  - A parent with no dates still appears when any of its children match.
  - A parent with its own dates shows its own bar.

We rejected stored, named Focus periods (shared, non-overlapping, sprint-like
ranges). Riley thinks in phrases that map to dates or to other items, not in
fixed periods.

## Consequences

- This reverses the old "a missing date is open-ended, never excluded" rule
  in [project_design.md](../../project_design.md#time-filtering).
- Quick picks that depend on the date must use the server's configured time
  zone, not UTC.
