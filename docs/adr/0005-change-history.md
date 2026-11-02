# 5. An append-only change history, written by the Application layer

Status: accepted (2026-11-02)

## Context

Work items have no creator and no "last changed by", and Weaver keeps no
record of changes. That leaves two gaps:

- Nobody can see what an agent did, as distinct from what a human did.
- An agent can't ask "what changed since yesterday", and deletes leave no
  trace.

## Decision

- **Every change to a work item appends a history entry.** That includes
  create, every field change, status, parent, links, comments, delete and
  restore.
- **Each entry records:**
  - when the change happened
  - the account
  - the API key, for agents
  - the actor label, if one was sent
  - the field
  - the old value
  - the new value
- **The Application services write it**, not the controllers or MCP tools.
  REST and MCP therefore can't diverge.
- **The UI shows it** in a History tab on the work item.
- **Agents read it** through a "changes since T" query and through
  `get_work_item_context`'s `include`.

## Consequences

- Every mutating service method needs to know the caller: account, key and
  label.
- Recording deletes in history is what makes them visible to "changes since"
  (see [ADR-0007](0007-trash.md)).
- Reminders through ntfy can later subscribe to history entries, without a
  second event system.
- History grows without limit. Pruning can be decided once it actually
  matters.
