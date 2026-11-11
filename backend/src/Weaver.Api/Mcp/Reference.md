# Weaver reference for agents

Weaver is a self-hosted work tracker shared by a household and its agents. It
holds everything from a renovation to a chore to a software bug, as trees of
work items grouped into Spaces. People use the app (a Board and a Roadmap);
agents use these MCP tools. Both see the same data and the same
history.

## Vocabulary

**Work item.** Anything tracked, at any level. Work items form trees through
their parent. Don't call them tasks or tickets; a "card" is just how the Board
draws one.

**Number.** A work item's human-facing `#N`, unique across the server. It
isn't the item's id. When someone says "#123", look it up with
`get_work_item` (`number: 123`) or `search_work_items` (`text: "#123"`).

**Space.** A top-level container such as Home, Village or Software. Each item
belongs to exactly one Space. A top-level item is created in one; children
share their parent's. A Space owns its statuses, priorities and Types, and an
item only takes its own Space's. Reparenting never crosses a Space: moving to
another Space is its own step, which takes the sub-items along and maps their
statuses, Types and priorities.

**Type.** A per-Space kind of work item (e.g. Room, Chore, Bug), with a colour
and its own custom fields. Types don't limit nesting. Tool names keep a
work-item prefix, as in `list_work_item_types`.

**Custom field.** A field a Type defines: text, Markdown, number, currency,
date, checkbox, single select, multi-select, user or URL. Only admins define
them. Changing an item's Type clears its values (history keeps them).

**Roll-up.** A setting on a number or currency field: a parent shows the sum of
that field over its live descendants, never including its own value.

**Status category.** ToDo, Doing or Done. Every status has one.

**Completion date.** The day an item entered a Done-category status. Moving
between Done statuses keeps it; leaving Done clears it.

**Done-hiding period.** Per Space, in days (`doneHidingDays`). The Board hides
Done items completed longer ago than this.

**View.** A saved, shared query over one or more Spaces with filters and a
layout. It stores no dates: the window is chosen when it's shown. **Overview**
is the built-in View of everything in a Doing status or with dates.

**Board.** The swimlane layout of a View: each parent is a lane, its children
are cards in status columns. "Board order" is the order cards appear in.

**Agenda.** A list layout of dated items grouped by day: Overdue (not Done and
ended before today), Today, then each later day, in the server's time zone.

**Backlog.** Every item that isn't Done and has no dates.

**When.** An optional free-text phrase on an item ("before the Christmas
market", "after drywall") recording the intent behind its dates.
**Resolving** a When sets the item's dates and/or adds a blocked-by link; the
phrase is kept. Until then it's **unresolved**.

**Tag.** A free-text label. Tags are global across Spaces.

**Link.** A typed relationship between two items, possibly in different
Spaces: Related, Blocks / BlockedBy, or Duplicates / DuplicatedBy. A blocks
link is stored once and each side reads it from its own view. Two items have
at most one link. A **URL link** points outside Weaver (a PR, a quote).

**Attachment.** A file on a work item. Agents can list and fetch attachments
but not upload or delete them.

**Dependency conflict.** A blocked item scheduled to start before its blocker
ends. Starting on the blocker's end date isn't a conflict, nor is anything
missing a date or already Done. Weaver flags conflicts and never reschedules
anything; neither should you.

**History.** The append-only record of every change: who (account, API key and
actor label), when, which field, old and new value. It outlives deleted items.

**Changes since T.** The items whose history has entries at or after a moment
T, each with those entries, including deleted ones.

**Summary.** An item in brief, with names instead of ids, as lists return it.
Full detail comes from the item itself.

**Trash.** Where deleted items wait until the retention period ends. Normal
reads leave them out. Read-write agents can restore from it; only humans can
purge it.

**Agent account / API key.** Your account signs in with an API key, which is
**read-only** or **read-write**. A read-only key can't call any write tool.

**Actor label.** Optional text you send with a write (e.g. "sub-agent:
test-writer"). History records it, so sub-agents sharing one connection can be
told apart.

## Conventions

- **Ids and Numbers.** Tools take ids (GUIDs). Only `get_work_item`,
  `get_work_item_context` and `search_work_items` also take a Number. Turn
  "#123" into an id with one of those first.
- **Summaries, then detail.** List and search tools return summaries with
  names and leave out empty fields. Use `get_work_item` or
  `get_work_item_context` for the rest.
- **Patch semantics.** On `update_work_item_details`, `reschedule_work_item`,
  `set_work_item_tags` and `set_work_item_fields`, a field you leave out is
  unchanged and `null` clears it. Status, parent, assignee, tags, dates and
  custom fields each have their own tool. Other tools aren't patches: leaving
  out `userId` in `assign_work_item` unassigns the item, leaving out
  `parentId` in `reparent_work_item` makes it top-level, and leaving out
  `afterId` places an item first.
- **expectedVersion.** `get_work_item` and `get_work_item_context` return an
  item's `version` (summaries don't). Pass it to a
  write as `expectedVersion`; if the item changed since, the write is rejected
  ("changed by someone else") instead of overwriting the other change. The
  tools that take `expectedVersion` return the item with its new `version`
  and the list of changed fields, so you can chain writes without
  re-reading. `create_work_item` returns the new item with its version.
- **Dates** are calendar dates, `yyyy-MM-dd`. Moments (`since`,
  `updatedSince`) are ISO 8601 timestamps with an offset. "Today" is the
  server's time zone, from `get_server_settings`.
- **Paging.** `search_work_items`, `run_view`, `list_unresolved_when_items`
  and `list_changes_since` return a `nextCursor` while more remains: call again
  with the same arguments and that cursor. `list_all_work_items` pages by
  `offset`.
- **Admin settings are read-only to agents.** Spaces, statuses, priorities,
  Types, fields, users and server settings can be read but only changed by an
  admin in the app.

## Tools

### Orientation
- `whoami`: your account, and your API key's label and scope.
- `get_server_settings`: the server time zone and Trash retention.
- `list_spaces`: every Space, with its done-hiding period.
- `list_statuses`: a Space's statuses, in board order, with categories.
- `list_work_item_priorities`: a Space's priorities, least to most urgent.
- `list_work_item_types`: a Space's Types, with their custom fields and
  options.
- `list_users`: every user, e.g. to assign an item.

### Finding and reading work
- `get_work_item`: one item in full detail, by id or Number, with roll-ups.
- `get_work_item_context`: one item plus whatever you `include`: ancestors,
  children, comments, links, history, attachments.
- `search_work_items`: the server-side query: Spaces, text, Number, status
  category, assignee, tags, Types, date window, Backlog, done-hiding,
  updated-since, Trash.
- `list_work_item_children`: an item's direct children, or a Space's
  top-level items, in board order.
- `list_all_work_items`: every item in a Space (or all), flat, to rebuild the
  tree from `parentId`.
- `list_views`: the saved Views, Overview first.
- `run_view`: the items a View shows, for an optional date window.
- `list_changes_since`: items changed at or after a moment, with who changed
  what; can leave out your own changes.

### Changing work
- `create_work_item`: a new item in a status, under a parent or at the top of
  a Space. Set dates, When, assignee, tags and fields afterwards.
- `update_work_item_details`: title, description, Type, priority and When.
- `reschedule_work_item`: start and/or end date.
- `change_work_item_status`: move to another status column.
- `reparent_work_item`: move under another parent in the same Space.
- `assign_work_item`: set or clear the assignee.
- `set_work_item_tags`: replace the tag list.
- `set_work_item_fields`: set or clear custom field values.
- `plan_work_item_space_move`: preview a move to another Space, without
  changing anything.
- `move_work_item_to_space`: move an item and its subtree to another Space,
  mapping statuses, Types and priorities.

### Links
- `list_work_item_links`: an item's links to other items, from its side.
- `create_work_item_link`: link two items (Related, Blocks, BlockedBy,
  Duplicates, DuplicatedBy).
- `delete_work_item_link`: remove a link.
- `list_work_item_url_links`: an item's links outside Weaver.
- `create_work_item_url_link`: add one.
- `delete_work_item_url_link`: remove one.

### Comments and attachments
- `list_comments`: an item's comments.
- `add_comment`: comment as your account.
- `update_comment`: edit your own comment.
- `delete_comment`: delete your own comment.
- `list_work_item_attachments`: an item's attachments' metadata.
- `get_work_item_attachment`: fetch one attachment's content (up to 5 MB).

### Repeating work
- `list_repeating_work_items`: every repeating item, with its next
  occurrence.
- `get_work_item_recurrence`: one item's schedule.
- `set_work_item_recurrence`: make an item repeat, or change its schedule.
- `remove_work_item_recurrence`: stop it repeating.

### Planning checks
- `list_unresolved_when_items`: items with a When but no dates and no blocker.
- `list_dependency_conflicts`: blocked items that start before their blocker
  ends.

### Trash
- `delete_work_item`: move an item (and, with `cascade`, its subtree) to the
  Trash.
- `list_trash`: what's in the Trash, newest first.
- `restore_work_item`: bring a deleted item and its subtree back.

## Workflows

### Find an item someone names by Number
1. `get_work_item` with `number` (no `#`), or `get_work_item_context` to also
   get its parent chain, children, comments or history.
2. Use the returned `id` for any write, and its `version` as
   `expectedVersion`.

### Add work
1. `list_spaces`, then `list_statuses` (and `list_work_item_types`,
   `list_work_item_priorities`) for the Space.
2. `create_work_item` with a status from that Space and either `spaceId` (top
   level) or `parentId`.
3. Set dates with `reschedule_work_item`, a When with
   `update_work_item_details`, then assignee, tags or fields with their tools,
   passing each write's returned `version` on to the next.

### Resolve When phrases
1. `list_unresolved_when_items`, optionally for some Spaces.
2. A phrase that names a time ("next spring", "before the 20th"): work out the
   dates in the server time zone (`get_server_settings`) and call
   `reschedule_work_item`.
3. A phrase that names other work ("after drywall"): find that item with
   `search_work_items`, starting in the same Space. If exactly one item clearly
   matches, `create_work_item_link` with `type: BlockedBy` and
   `targetWorkItemId` set to it.
4. If the phrase is ambiguous, matches several items or dates, or matches
   none, leave it unresolved and ask a human (or comment on the item). The
   phrase is always kept.

### Report dependency conflicts
1. `list_dependency_conflicts`, optionally for one Space.
2. For each, tell a human which item starts before its blocker ends, or
   `add_comment` on the blocked item. Which date is wrong is a person's call:
   never reschedule either item to make the conflict go away.

### Catch up on what changed
1. `list_changes_since` with a timestamp (e.g. yesterday morning), and
   `excludeOwnChanges: true` to see only what others did.
2. Deleted items come with `deleted: true`, restored ones with
   `restored: true`. Use `get_work_item` for a live item's current state.

### Handle a write that was rejected as a conflict
1. Re-read the item (`get_work_item`).
2. If your change still makes sense on top of theirs, retry with the new
   `version`. If the two changes disagree (e.g. both set the dates), ask a
   human rather than overwriting their change.

### Move work to another Space
1. `plan_work_item_space_move` to see what moves and how statuses, Types and
   priorities map, and how many field values would be cleared.
2. `move_work_item_to_space`, passing maps for anything the defaults get
   wrong. Ask first if field values would be lost.

### Delete and restore
- Only `delete_work_item` uses the Trash. Deleting a comment, a link or a URL
  link is permanent.
- `delete_work_item` needs `cascade: true` for an item with children.
- `list_trash` then `restore_work_item` brings it back, under its parent if
  that still exists.

## When to ask a human

- A When phrase is ambiguous, or matches several items, several dates or none.
- `list_dependency_conflicts` reports a conflict.
- Your write was rejected because someone else changed the item, and the two
  changes disagree.
- A change would lose information: a Space move that clears field values, a
  Type change that clears fields, a cascading delete.
- Anything an admin owns: Spaces, statuses, Types, fields, users, settings.
