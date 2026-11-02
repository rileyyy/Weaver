# 6. One server-side work-item query, used by every client and by Views

Status: accepted (2026-11-02)

## Context

Search, tag filtering and the time-frame overlap rule are implemented only in
Flutter (`board_filters.dart`). Agents have no way to search, filter or look
up `#Number`. The only flat listing pages by an offset ordered by `Rank`, and
`Rank` changes whenever an item moves, so a reader paging through the list
can skip or repeat items. Riley also wants saved boards, for example one
that leaves out Software.

## Decision

- **One query in the Application layer** serves Flutter, MCP and Views. It
  takes:
  - Space(s)
  - free text
  - `#Number`
  - status category
  - assignee
  - tags, with OR or AND
  - Type
  - date window (by the rule in [ADR-0003](0003-when-and-backlog.md))
  - Backlog-only
  - updated-since
  - include-deleted
- **It pages with a stable cursor**, not an offset over a mutable order.
- **A View is a stored, shared instance of this query**, plus a layout
  (Board, Hierarchy, Roadmap or Agenda).
  - Each user picks a default View.
  - **Overview** is built in: items in a Doing-category status or with
    dates in the window, across every Space, grouped by Space.

## Consequences

- The client-side filter logic is deleted, which restores the rule that
  business logic doesn't live in the client.
- The query needs indexes for text search and date overlap. Postgres
  full-text search is enough at this scale.
