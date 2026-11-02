# 7. Deletes go to a Trash with configurable retention

Status: accepted (2026-11-02)

## Context

Every delete is permanent today, including cascading deletes of a whole
subtree. Agents are also allowed to delete, and they can make mistakes.

## Decision

- **Deleting a work item moves it, and its subtree, to the Trash.**
- **Trashed items are purged automatically** once the retention period
  passes. An admin sets the period server-wide on the Admin page.
- **Humans can view, restore and purge** the Trash.
- **Read-write agents can view and restore it, but not purge it.** Agents
  also can't change the retention period (see
  [ADR-0004](0004-agent-accounts-and-api-keys.md)).
- **Delete, restore and purge are recorded in history** (see
  [ADR-0005](0005-change-history.md)).

## Consequences

- Every normal read excludes trashed items. The query's include-deleted flag
  (see [ADR-0006](0006-server-side-query-and-views.md)) is the only way to
  see them.
- Restore has to handle a parent that's still trashed or has since been
  purged. The likely rule: restore the item under the nearest ancestor that
  still exists, or as a root of its Space.
- Links and comments on a trashed item are kept until it's purged.
