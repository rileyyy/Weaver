# 9. Typed links, URL links, and dependency flags rather than auto-scheduling

Status: accepted (2026-11-02)

## Context

Today a link is an untyped, symmetric "related to" association, and you add
one by typing the target's raw id. Two needs aren't covered:

- Renovations need an order, for example drywall before paint.
- Software work needs to point at PRs and commits, and home work at quotes
  and product pages.

## Decision

- **Work-item links have a type** from a fixed set: *related*, *blocks /
  blocked by*, and *duplicates*. They can cross Spaces.
- **A URL link** points outside Weaver and has an optional label.
- **Links are added through a search picker**, backed by the server query
  (see [ADR-0006](0006-server-side-query-and-views.md)), not by id.
- **The Roadmap draws "blocks" links as arrows.**
- **A dependency conflict is a blocked item that starts before its blocker
  ends.** Weaver flags it with a badge on the card and on the Roadmap, and
  agents can query for conflicts so they can raise them with a human.
  Weaver never moves dates by itself. A conflict usually means someone
  forgot to update a date, which is a person's call to fix.

We rejected two alternatives:

- **User-defined link types:** more configuration than any current use
  needs.
- **Automatic rescheduling:** complex and surprising.

## Consequences

- Resolving a When like "after drywall" creates a *blocked-by* link (see
  [ADR-0003](0003-when-and-backlog.md)).
- Deletes can't remove links any more, because the item is only trashed
  (see [ADR-0007](0007-trash.md)). Links are dropped only when the item is
  purged.
