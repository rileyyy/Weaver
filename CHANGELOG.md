# Changelog

What shipped, newest first. Design reasoning lives in
[docs/architecture.md](docs/architecture.md); open items in
[docs/open-questions.md](docs/open-questions.md).

## Milestone status

| Milestone | Status      | Result                                      |
| --------- | ----------- | ------------------------------------------- |
| 0         | Done        | Architecture + repo + Docker                |
| 1         | Done        | Database + domain model                     |
| 2         | Done        | API                                         |
| 3         | Done        | Flutter shell                               |
| 4         | Done        | Standalone swimlane prototype               |
| 5         | Done        | API-connected board                         |
| 6         | Done        | Hierarchy                                   |
| 7         | Done        | Time-frame filter                           |
| 8         | Done        | Search/filter/sort                          |
| 9         | Done        | Work-item details                           |
| 10        | Done        | Authentication                              |
| 11        | Done        | Comments/links                              |
| 12        | Skipped     | Attachments (now in 19)                     |
| 13        | Done        | MCP                                         |
| 14        | Partly done | Mobile refinement (rest in 19)              |
| 15        | Done        | Production deployment pieces                |
| 16        | Planned     | Foundations: Spaces, Types, history, agents |
| 17        | Planned     | Agent surface                               |
| 18        | Planned     | Human UX: Views, Backlog, When              |
| 19        | Planned     | Attachments, mobile polish, cleanup         |
| 20        | Later       | Reminders, export, autonomous agents        |

Milestones 9–11 were built in dependency order (10, then 9, then 11),
because Milestone 9's "assigned to" field needs Milestone 10's users.

## 2026-11-10 — Unresolved When items over MCP

`feature/unresolved-when-items-over-mcp` (ticket 28 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0003](docs/adr/0003-when-and-backlog.md)).

- **`list_unresolved_when_items`** lists the work items whose When is
  unresolved (a phrase, but no dates and no live item blocking them) as
  summaries with the phrase, Space and parent, optionally narrowed by
  Space and status category, paged like `search_work_items`.
- **Resolving uses the existing writes**: `reschedule_work_item` for a
  date, or `create_work_item_link` with `BlockedBy` for "after …"; the
  phrase is kept and the item leaves the list. The tool's description tells
  agents to search for a referenced item before linking and to leave
  ambiguous phrases for a human.
- **Summaries carry `when`** wherever they're returned (MCP lists, and the
  REST trash and changes responses), left out over MCP when there's none.
- No migration.

## 2026-11-06 — Roll-ups of number and currency fields

`feature/roll-ups` (ticket 15 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0002](docs/adr/0002-types-and-custom-fields.md) and its amendment).

- **A number or currency field can roll up**: admins tick "Roll up into
  parents" when adding or editing it on the Admin page.
- **A parent shows the sum over its sub-items** at every depth, of any
  Type, next to its own value and never including it. Trashed items don't
  count. Only sums are supported.
- **A currency field fixes one currency**, so a total is in that
  currency; two fields are never summed together, even with the same name.
- **The detail dialog** shows a total under its field, and totals of
  fields only sub-items of other Types have in "Totals of sub-items". **The
  Hierarchy** gets a Totals column when the Space has a field that rolls
  up.
- **API:** `GET /api/work-items/{id}/roll-ups`,
  `GET /api/custom-fields?spaceId=`, and `rollsUp` on custom field
  definitions. MCP's `get_work_item` returns `rollUps`.
- **Migration:** `AddCustomFieldRollUp` adds the `RollsUp` column, off for
  every existing field. No reset needed.

## 2026-11-06 — Move a work item between Spaces

`feature/move-item-between-spaces` (ticket 16 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0001](docs/adr/0001-spaces.md)).

- **Move to Space…** in the detail dialog moves an item and its whole
  subtree to another Space, under a chosen parent there or at its top
  level. It shows how many items move and how many custom field values are
  cleared, and each status, Type and priority they use next to the one it
  becomes, which can be changed before confirming.
- **Defaults:** a status maps to the target's status with the same name
  (ignoring case), else its first status of the same category, else its
  first status. A Type or priority maps to its namesake, else it's cleared.
  Custom field values are always cleared, as with any change of Type.
- **Kept:** links (they may cross Spaces), comments, attachments, tags,
  dates and the assignee. Sub-items in the Trash move too.
- **History** records the Space, status, Type, priority and cleared values
  on every moved item, attributed to whoever moved it. Reparenting still
  can't cross Spaces.
- **API:** `GET /api/work-items/{id}/space-move-plan?targetSpaceId=` and
  `POST /api/work-items/{id}/space-move` (`targetSpaceId`, `parentId`,
  `statusMap`, `typeMap`, `priorityMap`, `expectedVersion`). MCP adds
  `plan_work_item_space_move` (read-only keys too) and
  `move_work_item_to_space` (read-write keys only).
- **Known gap:** a sub-item created under a moving item during the move
  stays behind (ticket 48). No migration.

## 2026-11-05 — Attachments

`feature/attachments` (ticket 38 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0008](docs/adr/0008-images-first-deployment.md)). Thumbnails and
camera upload are ticket 39.

- **Files on work items**: the detail dialog attaches, downloads and
  deletes them. Each file is stored under the attachments path named by
  its id, never by its filename, so a name can't escape that directory;
  its name, type and size are in Postgres.
- **A size limit**, `Attachments__MaxFileSizeMegabytes` (25 MiB by
  default). A bigger upload gets 413 naming the limit.
- **Trash keeps them**: a trashed item's attachments stay, hidden, and
  come back when it's restored. Purge (ticket 20) isn't built yet; it
  will remove the files through `IAttachmentFileCleanup`.
- **History** records `attachment` added or removed, named by the file
  name.
- **API:** `GET`/`POST /api/work-items/{id}/attachments` (multipart, one
  `file` part), `GET /api/work-item-attachments/{id}/content` and
  `DELETE /api/work-item-attachments/{id}`. MCP's
  `list_work_item_attachments` and `get_work_item_attachment` are
  read-only: agents can't upload or delete.
- **Deployment:** `Attachments__Path` (default `/data/attachments`) is a
  new place the backend keeps state. The example compose mounts the
  `attachments` volume there, or a host directory set as
  `ATTACHMENTS_DIR`. The `backup` service still only dumps Postgres, so
  back the attachments up too (README, Backups and restore). The frontend
  image's nginx now accepts `/api/` request bodies up to 30 MiB.

## 2026-11-05 — Lean MCP summaries, and names in DTOs

`feature/lean-mcp-summaries` (ticket 29 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md)).

- **MCP lists return summaries**: `list_work_item_children` and
  `list_all_work_items` now return the same brief shape as
  `search_work_items`, `list_trash` and `list_changes_since`: Number,
  title, Space, parent id, status and Type names, dates, assignee name
  and child count, loaded in one query. Use `get_work_item` for full
  detail.
- **Nulls are left out of MCP results**, so an absent field means "no
  value", and `nextCursor` is absent on the last page. History entries
  keep a null old or new value, since there it records a cleared field.
  REST JSON still writes nulls.
- **`get_work_item` takes `number`** as well as `id`; give exactly one.
- **REST and detail responses carry names next to ids**: `spaceName`,
  `parentNumber`, `parentTitle`, `statusName`, `typeName`,
  `priorityName` and `assignedToUsername` on every work item, looked up
  in at most six queries per response however many items it holds.
  Summaries also gained `parentId`.

## 2026-11-05 — Flutter uses the server query

`feature/flutter-uses-server-query` (ticket 18 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0006](docs/adr/0006-server-side-query-and-views.md)).

- **The server decides what matches** the header's search, tags and time
  window. While a filter is set, the board, Hierarchy and Roadmap show
  only the items the query returned for the Space. The client-side
  filter logic is gone.
- **Tag filter: Any or All**, offered once two or more tags are selected.
- **Behaviour changes:** search no longer matches tags (use the tag
  filter), matches whole words or word prefixes rather than any substring,
  and a time window hides undated items (ADR-0003).
- **Hierarchy and Roadmap** load through the query's stable cursor instead
  of an offset over Rank, so an item moved mid-load is neither skipped nor
  repeated. Flutter no longer calls `GET /api/work-items/all`.

## 2026-11-04 — Typed links, URL links, and the link picker

`feature/typed-links-url-links-and-picker` (ticket 26 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0009](docs/adr/0009-typed-links-and-dependency-flags.md)).

- **Links have a type**: related, blocks / blocked by, or duplicates /
  duplicated by, and they can cross Spaces. A blocks link is stored once
  and each item reads it from its own side. Existing links became
  *related*. Two items still have at most one link; a blocks link against
  one that already runs the other way is rejected as a cycle.
- **URL links** point outside Weaver (a PR, a quote, a product page): an
  http or https address with an optional label.
- **A When counts as resolved** once a live item blocks the item, as well
  as when it has dates (ADR-0003). A blocker in the Trash stops counting.
- **History** records `link` on both items as `{ workItemId, type }`, the
  type read from each item's side (older entries hold just the id), and
  `urlLink` as `{ url, label }`, named by the label or the URL.
- **API:** `POST /api/work-items/{id}/links` requires `type`
  (`Related`, `Blocks`, `BlockedBy`, `Duplicates`, `DuplicatedBy`), and
  links carry `type`, `linkedWorkItemNumber` and `linkedWorkItemSpaceId`.
  `GET`/`POST /api/work-items/{id}/url-links` and
  `DELETE /api/work-item-url-links/{id}` are new. MCP's
  `create_work_item_link` takes `type`, and `list_work_item_url_links`,
  `create_work_item_url_link` and `delete_work_item_url_link` are new.
- **Detail dialog:** links are grouped by type and added by picking a type
  and searching every Space by title or `#Number`, instead of typing an
  id. A URL Links section lists, opens, adds and removes URL links.

## 2026-11-04 — Trash: delete and restore

`feature/trash-delete-and-restore` (ticket 19 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0007](docs/adr/0007-trash.md)). Purging and retention are ticket 20.

- **Deleting moves to the Trash**: the item and its sub-items disappear
  from every view, list, search and count, but nothing is lost. Links and
  comments are kept, hidden while the item is trashed.
- **The board's Trash tab** lists each delete with who made it and when,
  and how many items it holds, with Restore.
- **Restore brings back what went together**: the item and the sub-items
  deleted with it, but not a sub-item deleted separately before. It goes
  back under its parent, or the nearest ancestor that still exists, or to
  the top of its Space.
- **Repeating items:** a deleted copy is never recreated, deleting the
  repeating item stops its schedule, and restoring it resumes from today
  without catching up on the time it spent in the Trash.
- **History** records `deleted` and `restored`; "changes since T" marks
  items `deleted` (now in the Trash or purged) or `restored`.
- **API:** `DELETE /api/work-items/{id}` keeps its route and now trashes;
  `GET /api/trash` and `POST /api/trash/{id}/restore` are new. The search
  takes `includeDeleted`, and items and summaries carry `trashedAtUtc`.
  MCP adds `list_trash` (read-only keys too) and `restore_work_item`, and
  `search_work_items` takes `includeDeleted`.
- **Migration:** `WorkItemTrash` adds two nullable columns and an index.
  No reset needed.

## 2026-11-04 — Detail dialog: status, parent and sub-items

`feature/detail-dialog-status-parent-sub-items` (ticket 33 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md)).
App only; uses ticket 17's search.

- **Status** is a dropdown of the item's Space's statuses.
- **Parent** is picked by searching the item's Space, or set to none. A
  cycle is refused with a message naming the item in the way.
- **Add sub-item** creates a child from the dialog's Sub-Items section.
- The board reloads after the dialog closes when any of these changed.

## 2026-11-04 — Custom fields on Types

`feature/custom-fields-on-types` (ticket 14 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0002](docs/adr/0002-types-and-custom-fields.md)). Builds on ticket 13.

- **Each Type defines its own fields**: text, Markdown, number, currency,
  date, checkbox, single select, multi-select, user and URL. Admins add,
  rename, reorder and remove them, and edit select options, from a Type's
  row on the Admin page's Types tab.
- **The detail dialog edits every field** with an editor for its kind.
  Each value saves on its own, and the server rejects a value of the wrong
  kind, an unknown option or an unknown user.
- **Changing an item's Type clears its values**, and removing a field or
  option removes the values that used it. History keeps every cleared
  value.
- **API:** `/api/custom-fields` for definitions and
  `PATCH /api/work-items/{id}/fields` for values; work items carry
  `fields`. MCP `list_work_item_types` includes each Type's fields, and
  `set_work_item_fields` sets values. Agents can't change definitions.
- **Migration:** `CustomFieldsOnTypes` adds the definitions table and the
  items' values column. No reset needed.

## 2026-11-04 — Server-side work-item query, with MCP search

`feature/server-side-work-item-query` (ticket 17 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0006](docs/adr/0006-server-side-query-and-views.md)). Backend only;
the app switches to it in ticket 18.

- **Agents can find work items**: MCP `search_work_items` filters by
  Space, text, status category, assignee, tags (any or all), Type, date
  window, Backlog and last update, and `#123` looks up that item.
- **REST:** `GET /api/work-items/search` takes the same inputs and returns
  full items.
- **Text search** matches every typed word, or the start of it, in the
  title, description or When, using Postgres full-text search with an
  index.
- **The date window** shows items whose dates overlap it, leaves out
  undated items, and keeps an undated parent when something under it
  matches ([ADR-0003](docs/adr/0003-when-and-backlog.md)).
- **Paging is stable**: results come in `#Number` order with a cursor, so
  items moving between pages are neither skipped nor repeated.

## 2026-11-03 — Types replace Layers, per Space

`feature/types-replace-layers` (ticket 13 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0002](docs/adr/0002-types-and-custom-fields.md)). Builds on ticket 12.

- **Layer is now Type** everywhere: the app, REST (`/api/work-item-types`,
  `typeId`), MCP (`list_work_item_types`, `typeId`) and history.
- **Each Space has its own Types**, managed on the Admin page's Types tab
  with a Space picker. A new Space starts with Project / Goal / Task. An
  item only takes its own Space's Type (400 otherwise).
- **Any Type can sit under any other**, as before.
- **No reset needed:** existing items keep their Type, moved onto the
  matching Type in their own Space, and old history entries show as Type.
- **API:** listing, creating and reordering Types need a `spaceId`, as for
  statuses and priorities.

## 2026-11-03 — Per-Space statuses and priorities

`feature/per-space-statuses-and-priorities` (ticket 12 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0001](docs/adr/0001-spaces.md)).

- **Each Space has its own statuses and priorities**, so Home can use
  Quoted / Booked / Done while Software uses New / Active / Resolved. A new
  Space starts with To Do / Doing / Done and Low / Medium / High / Urgent.
- **A work item only takes its own Space's** status and priority; another
  Space's is refused (400), over REST and MCP alike.
- **Admins manage them per Space** on the Admin page: the Statuses and
  Priorities tabs have a Space picker. Names, order and the "at least one
  status" rule apply within each Space.
- **The board and the detail dialog** show only the item's Space's
  statuses and priorities.
- **API:** `GET /api/statuses` and `/work-item-priorities` need a
  `spaceId`, and so do creating and reordering; results carry `spaceId`.
  MCP `list_statuses` and `list_work_item_priorities` take a `spaceId`.
- **Reset needed:** the migration drops the global statuses and priorities
  and gives existing Spaces the defaults, so a database that already has
  work items must be reset.
## 2026-11-03 — Narrow board rebuilds

`feature/narrow-board-rebuilds` (ticket 37 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md); closes
code-review finding F-M7).

- **Editing one card rebuilds only its own lane.** Each lane listens to
  its own slice of the shared item store, so a status change, rename or
  assignment no longer rebuilds the header and every other lane.
- Hierarchy, Roadmap and the Filters dialog's tags still follow every edit,
  through their own signal.

## 2026-11-03 — Changes since T

`feature/changes-since` (ticket 31 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0005](docs/adr/0005-change-history.md)).

- **Agents can ask what changed since a moment.** `GET
  /api/work-items/changes?since=…` and the read-only MCP tool
  `list_changes_since` list the work items with history at or after
  `since`, each as a brief summary with names (number, title, Space,
  status, layer, dates, assignee, child count) and its entries since then.
- **Deletes are included and marked** `deleted: true`, with the title and
  Space the item had.
- **Filter by Space**, and **leave out your own changes**
  (`excludeOwnChanges`) to see what others did. Changes Weaver makes
  itself, such as generating repeat copies, are always kept.
- **Pages with a stable cursor** (`limit` up to 200, default 50; pass
  `nextCursor` back). Changes made between pages don't shuffle or repeat
  items.
- Restored items aren't marked yet: there is no restore until the Trash
  (ticket 19).

## 2026-11-03 — Spaces replace Boards

`feature/spaces-replace-boards` (ticket 11 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0001](docs/adr/0001-spaces.md)).

- **Every work item belongs to a Space.** A top-level item is created in
  a chosen Space (`spaceId` on `POST /work-items` and `create_work_item`);
  a child joins its parent's. Repeat copies stay in their template's
  Space.
- **Reparenting can't cross a Space** (409), over REST and MCP alike.
- **Top-level listings take a Space.** `GET /work-items` and
  `/work-items/swimlanes` need a parent/scope item or a `spaceId`;
  `/work-items/all` and `list_all_work_items` filter by an optional one.
- **Admins create and rename Spaces** (`/api/spaces`, Admin page → Spaces).
  Anyone can list them, agents included (`list_spaces`), but no agent can
  change them.
- **History records an item's Space** when it's created, by name in the
  History tab, and a Space's creation and renames go to its own history.
- **The app shows one Space at a time** and remembers the last one on the
  device. The breadcrumbs start at the Space's name, with a switcher in
  front. Hierarchy and Roadmap show the current Space.
- **The Board entity is gone**, with `/api/boards` and the board MCP tools,
  and deleting an item no longer checks for a board scoped to it.
  Statuses, priorities and layers stay global for now (tickets 12, 13).
- **Reset local databases.** The migration is a clean schema change with
  no data migration: a database that already has work items can't be
  upgraded, since they have no Space.

## 2026-11-03 — When field and quick picks

`feature/when-field-and-quick-picks` (ticket 25 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0003](docs/adr/0003-when-and-backlog.md)).

- **Work items have an optional free-text When** ("after drywall", up to
  200 characters). It's patched with the other details over
  `PATCH /work-items/{id}/details` and `update_work_item_details`, is
  recorded in history, and is copied onto repeat occurrences. A blank
  When clears it.
- **Items report `whenResolved`**: true once a When is backed by a start
  or end date. Typing a When never sets dates, so it stays unresolved
  until someone (or an agent, ticket 28) gives it dates.
- **Quick picks are resolved on the server.** `GET /quick-picks` gives
  this weekend, next week and next month as dates for today in the
  server time zone; weeks run Monday to Sunday.
- **The detail dialog has a When field** with a Resolved/Unresolved badge
  and the three quick picks. The phrase saves on Enter or when the field
  loses focus; a quick pick sets the item's dates and leaves the phrase
  as typed.

## 2026-11-03 — Board writes send `expectedVersion` through a shared item store

`feature/shared-item-store-with-expected-version` (ticket 36 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md); closes
the gap left by code-review findings B-H3 and F-H6).

- **A board edit can't silently overwrite someone else's change.**
  Moving, reordering, reparenting, assigning and tagging from the Board,
  Hierarchy and Roadmap send the item's version. If someone else changed
  the item first, it snaps back to the latest version and the board says
  so; other cards being saved at the same time are left alone.
- **One copy of each item.** The Board, Hierarchy, Roadmap and detail
  dialog read the same shared store, so an edit in the dialog shows on
  every layout as soon as it's saved, without reloading the board when the
  dialog closes. Deleting an item removes it everywhere.
- **Quick successive edits no longer conflict with each other.** A second
  edit to the same item (e.g. a tag change while a drag is saving, or a
  delete right after a save) waits for the first and sends the version it
  returned.
- The board still reloads after repeat settings change, since those can
  create items.

## 2026-11-02 — Admin UI for agent accounts and keys

`feature/admin-ui-for-agent-accounts` (ticket 08 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).

- **The Admin page has an Agents tab.** Admins add an agent with a
  free-text name and optional description, and deactivate or reactivate
  it; the confirmation says deactivating doesn't revoke its keys.
- **Each agent has a keys page.** Issuing a key takes a label and a scope
  (read-only or read-write, each explained) and shows the key once, with a
  copy button and a warning that it won't be shown again. Keys list their
  scope, when they were created and last used; revoking asks first, and
  revoked keys stay listed, marked revoked.
- **The Users tab lists people only**, since its rename, password and
  admin actions don't fit agents.
- **Assignee pickers mark agents** with a badge, on the board and in the
  detail dialog.

## 2026-11-02 — Markdown and task lists

`feature/markdown-and-task-lists` (ticket 35 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md)).

- **Descriptions and comments render Markdown.** The description shows
  rendered by default; its edit button switches to the raw text, and
  edits are still saved with **Save details**. Editing a comment shows
  its raw text.
- **Task lists in a description can be ticked.** `- [ ] milk` draws a
  checkbox; ticking it changes exactly that line and saves just the
  description with `expectedVersion`. If someone else changed the item
  first, the dialog reloads it and says so. History records the tick as a
  description change. Boxes in comments, and in a description with
  unsaved edits, are read-only.
- **Rendered Markdown is safe to show**: raw HTML is shown as text,
  images show their alt text instead of being fetched, and only `http`,
  `https` and `mailto` links open, outside the app.
- New packages: `flutter_markdown_plus` (the maintained fork of the
  discontinued `flutter_markdown`), `markdown` and `url_launcher`.

## 2026-11-02 — Change history with actor

`feature/change-history-with-actor` (ticket 10 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0005](docs/adr/0005-change-history.md)). Stacked on ticket 09's
branch.

- **Every change to a work item is recorded**: create, each field
  change, status, parent, comments, links, repetition and delete, including
  side effects such as items moved off a deleted status or unassigned from
  a deactivated user. Each entry has when, the account, the API key for
  agents, the actor label, the field and the old and new value. A write
  that fails or changes nothing records nothing. Board reordering (rank)
  isn't recorded.
- **`GET /api/work-items/{id}/history`** lists an item's history newest
  first, with names for statuses, users and linked items. It keeps working
  after the item is deleted. Nothing can edit or delete an entry.
- **The detail dialog has a History tab**, marking agents with a badge and
  showing the actor label they sent.
- **Actor labels:** every MCP tool that changes a work item takes an
  optional `actorLabel`; REST clients can send `X-Actor-Label`.
- Repeating-item occurrences generated in the background are recorded as
  made by Weaver itself.
- **An account that has changed any work item can't be deleted** (409), only
  deactivated, so history never loses who made a change.
- Comments take their author from the signed-in caller rather than an id
  each controller and tool passed by hand.

## 2026-11-02 — Key scopes and agent admin restrictions

`feature/key-scopes-and-agent-admin-restrictions` (ticket 07 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).

- **Read-only keys can't write.** Every REST request other than GET, HEAD
  or OPTIONS gets a 403 problem+json, and every MCP tool not marked
  read-only returns a tool error with the same message. New endpoints and
  tools are covered without opting in.
- **Read-write keys write like a non-admin person**: work items, comments,
  links, repeating items and boards.
- **No agent can change admin data**, whatever its key's scope. The admin
  check refuses every agent account, and an agent can no longer be made an
  admin (409). An agent flagged as admin earlier gains nothing from it and
  doesn't count towards keeping one admin.
- **An agent can't be given a password** through
  `PUT /api/admin/users/{id}/password` (409); it was stored and never used.

## 2026-11-02 — Patch updates and `expectedVersion` on every write

`feature/patch-semantics-and-expected-version` (ticket 09 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md)).

- **Field updates are patches.** Details, schedule and tags change only
  the fields sent, over REST (now `PATCH`) and MCP; `null` clears a field.
  An agent renaming an item no longer wipes its description, layer and
  priority.
- **Every work-item write takes an optional `expectedVersion`**, including
  status, parent, assignee and delete, which were last-write-wins. A stale
  one returns 409.
- **Writes return `{ item, changedFields }`**: the item with its new
  version, and the fields the write actually changed.
- The detail dialog now also sends its version when assigning and
  deleting. The board doesn't send versions yet (ticket 36).

## 2026-11-02 — Agent accounts and API keys

`feature/agent-accounts-and-api-keys` (ticket 06 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).

- **Admins create agent accounts** (a name and a description, no password)
  at `/api/admin/agents`, and can deactivate and reactivate them.
- **Agents sign in with API keys.** Each key has a label and a scope
  (`ReadOnly` or `ReadWrite`), is shown once when it's issued and stored
  only as a hash, and can be revoked. A client sends it as
  `Authorization: Bearer wvr_…` and never has to refresh it. Revoked keys
  and keys of deactivated agents get a 401 straight away.
- **Agents can't log in with a password**, and humans can't have keys.
- **MCP gains `whoami`**, naming the account, key and scope in use. The
  README shows a `.mcp.json` that reads the key from `WEAVER_API_KEY`.
- The caller context now carries the key. Scopes aren't enforced yet
  (ticket 07), and there's no Admin page for agents yet (ticket 08).

## 2026-11-02 — Admins create every account

`feature/admin-only-account-creation` (ticket 04 of
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md),
[ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).

- **Self-registration is removed.** `POST /api/auth/register` and the login
  screen's "Create one" toggle are gone; the login screen says to ask an
  admin for an account.
- **The first admin comes from configuration.** Until now the first
  registered account became the admin. Now the backend creates the admin
  named by `FirstAdmin:Username` / `FirstAdmin:Password`
  (`WEAVER_FIRST_ADMIN_*` in `.env`) when it starts on a database with no
  accounts. Development uses `admin` / `development-only-admin-password`.
- **The user list is cached until invalidated**, like statuses, instead of
  re-fetched every five minutes; the refresh existed only for people
  registering while the app was open.
- Integration tests create their accounts through `/api/admin/users` as
  the configured first admin.

## 2026-11-02 — Plan for Spaces, Types and agents

`feature/plan-spaces-and-agents`, docs only. A design review after an
audit of the code planned Milestones 16–20; the decisions are in
[the spec](.scratch/spaces-and-agents/spec.md), nine ADRs in
[docs/adr/](docs/adr/) and a new [GLOSSARY.md](GLOSSARY.md).

- `project_design.md` gains rules for Spaces, Types, history, agent
  accounts and the Trash, and the time-filtering rule changes: from
  Milestone 16, items with no dates live in the Backlog instead of
  matching every time frame.
- `docs/open-questions.md` moves the questions the review settled into a
  "Settled on 2026-11-02" list pointing at the ADRs.
- `.scratch/` is no longer gitignored, so specs and tickets are committed.
- Milestone 14's remaining mobile checks and the skipped attachments
  milestone are folded into Milestone 19. Milestone 15 is marked done:
  its deployment pieces shipped and its remaining question, the access
  model, is settled in ADR-0004 and ADR-0008.

## 2026-10-19 — Board refinements

`feature/swimlane-drill-down-and-layer-colors`, stacked on
`feature/mobile-refinement` because it changes the same API repositories,
board card and test fakes that branch rewrote.

- **Roomier swimlane cards:** 12px between cards and around them in each
  cell, so each card is slightly smaller.
- **Drill down from the breadcrumb:** tapping the current crumb ("Board"
  at the top) lists its lanes; picking one makes it the board's top level.
  "View sub-items" in the detail dialog still works.
- **Layer colours:** each layer has a colour, set on the Admin page's
  Layers tab. It colours the bar on the left of a card (neutral when an
  item has no layer) and underlines titles in the Hierarchy view.
  Migration `AddWorkItemLayerColors` seeds Project / Goal / Task with pink,
  teal and green (the old bar colour), and other layers with blue-grey.

## 2026-10-09 — Milestone 14, mobile refinement (first pass)

`feature/mobile-refinement`. Aimed at sideloading a release APK and
reaching the backend over Tailscale.

- **Long press to move a card** on Android and iOS, native or in the
  browser, so swipes that start on a card scroll the board. Desktop keeps
  the immediate drag. The title tooltip no longer opens on long press
  there.
- **Server address on the sign-in screen** for native builds, with https
  assumed when no scheme is typed. It's remembered after a successful
  sign-in and prefilled next time; `API_BASE_URL` is now an optional
  starting value rather than required. Web is unchanged.
- **Release APKs can reach the network:** the main Android manifest
  lacked the `INTERNET` permission that only debug and profile builds
  had. The launcher label is now "Weaver".
- **TLS failures** (an untrusted certificate, say) show the "could not
  reach the server" message instead of failing silently.
- Not yet done: checking safe areas, the keyboard and back navigation on
  a real phone; touch-friendly column resizing in Hierarchy (deferred);
  iOS.

## 2026-09-28 — Admin page

Requested before Milestone 14 (`feature/admin-page`, stacked on
`feature/pre-milestone-14-improvements` because it edits the priority
table that branch introduced).

- **Administrators.** `User.IsAdmin`; existing accounts became admins in
  migration `AdminUsersAndDeactivation`, and on a fresh database the first
  registered account is the admin. The server checks it on every admin
  request; the last active admin can't be demoted, deactivated or deleted.
- **Admin page** behind a gear next to sign-out, shown only to admins,
  with four tabs:
  - **Statuses:** add, edit (name, category, colour), drag to reorder,
    delete with a replacement status for its items.
  - **Priorities / Layers:** add, rename, reorder, delete with a
    replacement or none.
  - **Users:** add, rename, reset password (also unlocks and signs out),
    unlock, grant/revoke admin, deactivate/reactivate, and delete for
    users with no comments.
- **Deactivated users** can't sign in or refresh, are unassigned from
  their work, drop out of assignee pickers and can't be assigned again;
  their comments keep their name.
- **API:** `POST`, `PUT /{id}`, `PUT /order`, `DELETE /{id}?replacementId=`
  on `/api/statuses`, `/api/work-item-priorities` and
  `/api/work-item-layers`; `/api/admin/users` for accounts. `UserDto`
  gained `IsAdmin`. Not exposed over MCP yet.
- Verified against Postgres (integration tests for the reorder swap,
  moving items off a deleted status, deactivation and delete, plus the
  migration's Up and Down on the dev database) and in headless Chromium:
  gear shown only to admins, add/drag/delete a status with the board
  showing the change, and deactivating a user at phone width.

## 2026-09-27 — Open-question improvements

Requested round before Milestone 14 (`feature/pre-milestone-14-improvements`),
settling four entries from [open-questions.md](docs/open-questions.md).

- **Configurable priorities.** Priority is a `WorkItemPriorities` lookup
  table seeded with Low / Medium / High / Urgent, like layers.
  `WorkItemDto.Priority` (an enum name) became `PriorityId`; new items have
  no priority unless one is set (they used to default to Medium). `GET
/api/work-item-priorities` and the `list_work_item_priorities` MCP tool
  list them. Migration `ConfigurablePriorities` keeps every existing
  item's priority; Up and Down were verified against the dev Postgres.
- **Quick tagging.** A "+" at the end of the tags on board cards, Hierarchy
  rows and Roadmap rows opens an Add tags dialog with existing tags as
  suggestions.
- **Hierarchy caret indents** with its title inside the Title column; the
  other columns stay aligned with their headers.
- **`list_all_work_items` MCP tool**, paging like `GET /work-items/all`.
- Checked in headless Chromium against the dev stack: tag added from a
  card and persisted, Hierarchy and Roadmap "+" dialogs, caret
  indentation, and the migrated priority in the detail dialog.

## 2026-09-26 — Repeating work items

Requested feature round before Milestone 14 (`feature/repeating-work-items`).

- **Repeat settings on a work item.** A Repeat section in the detail
  dialog sets an item to repeat Weekly or Bi-weekly on chosen weekdays
  (seven checkboxes, Monday first), or Monthly, Quarterly or Yearly on the
  start date's day of the month, between a start date and an optional end
  date. It's saved explicitly with **Save repeat**, and closing with an
  unsaved schedule asks first.
- **Occurrences are generated a week ahead.** A copy of the item (title,
  description, layer, priority, assignee, tags and its whole sub-item tree)
  is created under the item's current parent in the first status, dated on
  the occurrence day. Past copies stay on the board for reference. A
  background worker runs at startup and hourly; saving a schedule also
  generates immediately.
- **A Repeating tab** next to Swim Lanes, Roadmap and Hierarchy lists every
  repeating item with its lane, schedule and next date, where each can be
  edited or stopped.
- **API and MCP:** `GET/PUT/DELETE /api/work-items/{id}/recurrence`,
  `GET /api/recurrences`, and matching MCP tools. `WorkItemDto` gained
  `RecurrenceSourceId` / `RecurrenceDate`.
- New table `WorkItemRecurrences` (migration `AddWorkItemRecurrences`).
  Verified against Postgres: generation, idempotent re-saves, occurrence
  numbering in date order, and deleting a template (cascades its schedule,
  keeps its copies). UI checked in headless Chromium at desktop and phone
  widths.

## 2026-09-25/26 — Code review fixes

Fixes for the [2026-09-25 code review](code_review_findings.md), each on
its own branch merged to `master`.

### Backend

- **Deleting linked items and board scopes** (B-H2,
  `bugfix/delete-linked-work-item`): deleting a work item removes its links
  (and its cascaded descendants' links) instead of failing with a 500;
  deleting a board's scope item returns 409. Verified by hand against
  Postgres.
- **Lost updates** (B-H3, `bugfix/work-item-lost-updates`): `WorkItemDto`
  carries `Version`; details, schedule and tags accept `ExpectedVersion`
  and return 409 when it's stale. The detail dialog sends it and reloads
  on conflict. Status, parent and assignee stay last-write-wins.
- **Refresh-token reuse** (B-H4, `bugfix/refresh-token-reuse-detection`):
  reusing a rotated token revokes the chain issued from it, with a 30 s
  grace window; `RefreshToken` has an `xmin` concurrency token; the
  lifetime comes from `JwtOptions`; expired tokens are purged on
  login/refresh.
- **Request validation** (B-M1, `bugfix/request-validation`): titles,
  comment bodies and board names are required and length-checked; tags
  are capped at 20 per item and 50 characters each. Failures are 400 via
  `DomainValidationException` in REST and MCP.
- **Schedule dates** (F-H5, backend half): `StartDate` / `EndDate` became
  `DateOnly` (`date`), with a migration that recovers the originally
  picked day.

### Frontend

- **Token refresh stampede** (F-H1, F-M1,
  `bugfix/token-refresh-stampede`): concurrent requests share one refresh;
  a failed refresh or a 401 only clears the session it started with;
  tokens count as expired 30 s early.
- **Retry replaying actions** (F-H2, F-L9,
  `bugfix/board-retry-replays-create`): `retry()` only re-attempts a failed
  navigation; `refreshCurrentScope()` reloads the board; a failed create
  keeps the board and never becomes the retry target.
- **Board state races** (F-H3, F-H4, `bugfix/board-state-races`): scope and
  hierarchy loads carry generation numbers so stale responses are dropped;
  optimistic rollbacks revert only the changed item.
- **Dates one day early east of UTC** (F-H5,
  `bugfix/schedule-date-timezone`): schedule dates are sent as
  `yyyy-MM-dd` and held as local midnight; other timestamps are shown in
  local time. Tests pass under UTC+2, UTC+9 and UTC−7.
- **Stale board after edits** (F-H6, `bugfix/board-stale-after-edits`):
  the detail dialog reports changes (including from nested sub-item
  dialogs) and the board refreshes; moves, reparents and reschedules also
  update the Hierarchy/Roadmap data.

### Deployment and infrastructure

- **TLS and exposure** (I-H1, I-M3, I-L3,
  `bugfix/deploy-exposure-and-tls`): Caddy with an internal CA terminates
  TLS on 80/443 and redirects HTTP; the backend publishes no port; nginx
  proxies `/mcp`; the API honours forwarded headers and dropped
  `UseHttpsRedirection`; `.env.example` ships empty values; signing keys
  under 32 bytes are rejected; image variables are required. Verified with
  the production images: login, `/api` and an MCP handshake over HTTPS,
  with the backend port unreachable.
- **Health-ordered startup** (I-M1, part of B-M8,
  `feature/health-checks`): `/health` with a database check; db → backend
  → frontend → caddy wait on `service_healthy`.
- **Backups** (I-M4, `feature/database-backups`): a `backup` service dumps
  the database at startup and nightly, keeping 7 daily and 4 weekly dumps.
  Restore verified end to end.
- **CI** (I-M2, `feature/ci-hardening`): format check, `flutter analyze`,
  coverage artifacts for both stacks, image builds on PRs without pushing,
  Flutter pinned to 3.44.0 in CI and both Dockerfiles. The frontend was
  reformatted in its own commit.
- **nginx hardening** (I-L4, `feature/nginx-hardening`): unprivileged
  image, CSP and security headers, CanvasKit served locally, `no-cache`
  revalidation for app files. Verified by signing in through the UI in
  headless Chromium with no CSP violations.
- **`CLAUDE.md`** (I-L1, `feature/rename-claude-md`): renamed from
  `claude.md` for case-sensitive tooling.

### Docs

- Documentation restructured (I-L2): top-level README, `docs/`, this
  changelog; `agent_notes.md` retired.

## 2026-09-19 to 09-24 — Frontend rounds before Milestone 14

Requested polish rounds, not numbered milestones.

### Board UI and views (2026-09-19/20)

- Tapping a card opens a detail dialog with every attribute; drilling in
  moved to the dialog's "View sub-items" action. Swimlane labels open the
  lane's own item.
- Responsive header (breadcrumb, time filter, search, filters dialog,
  sign-out) and a board that fills the width, with only the columns area
  scrolling on narrow screens.
- "Add Work Item" button and dialog (new lane or card in an existing lane).
- Start/End date fields in the detail dialog; the card's calendar
  shortcut and its schedule dialog were later removed as redundant.
- Tabs: Swim Lanes, Roadmap (first called Calendar) and Hierarchy.
  `GET /work-items/all` added for Hierarchy.
- Delete with confirmation in the detail dialog.
- Per-status colours (`Status.Color`) on the board and Hierarchy.
- Swim-lane redesign in the style of Azure DevOps sprint boards:
  collapsible lanes, a self-sizing card grid, assignee avatars, status
  colour handles.
- Sequential work item numbers (`WorkItem.Number`, a Postgres identity
  column) shown as `#N`. Verified against Postgres: the migration numbered
  the existing rows and new items continued the sequence.

### View improvements

- Assignee avatars tappable everywhere to assign; an unassigned
  silhouette on swimlane labels and Hierarchy rows.
- Hierarchy columns (Number, Title, Status, Assigned To) with headers and
  draggable resize handles.
- Roadmap: a Gantt-style timeline with week/fortnight/month/quarter/year
  zoom, reusing the Hierarchy tree and filters; later weekly gridlines and
  a today marker.
- The local SDK at the time was too old to run the app, so this round was
  checked by unit tests only; the version pinning that followed in CI
  addresses that.

### Tags (2026-09-21)

- Free-text tags on work items (`text[]` column, set as a whole,
  normalised server-side), shown as badges on cards, Hierarchy and
  Roadmap; edited in the detail dialog; included in search; a tag filter
  in the Filters dialog.

### Cleanup (2026-09-23/24)

- Split multi-class files; chained DI registrations.
- Detail dialog redesigned as a responsive two-column layout with a
  Sub-Items section.

## Milestone 13 — MCP (2026-09-19)

- `IBoardService`, `IStatusService`, `IWorkItemLayerService` extracted
  first, so MCP could reuse services for every resource. The only REST
  change: an invalid board `ScopeItemId` returns the standard 404 body.
- MCP tools in `Weaver.Api/Mcp/` mirroring the controllers (work items,
  boards, statuses, layers, users, comments, links), stateless Streamable
  HTTP at `/mcp`, bearer-token auth through the existing fallback policy,
  string enums, domain exceptions translated to `McpException`.
- Verified against the dev stack: `initialize` and `tools/list` with a
  real token, `"Priority":"High"` in a `create_work_item` result, a
  readable error for a nonexistent id, and 401 without a token. This run
  caught an options bug that stopped the API from starting.

## Milestone 12 — Attachments

Skipped.

## Milestone 11 — Comments and links (2026-09-19)

- Comments on work items, editable and deletable only by their author.
- Symmetric work-item links: listed from either side, duplicates in
  either direction and self-links rejected. Added by raw id.
- Verified in the dev stack: comments render with author and time and
  own-comment controls; links appear under "Related work items".

## Milestone 9 — Work-item details (2026-09-19)

- `WorkItemLayer` lookup table (Project / Goal / Task), `Priority` enum,
  assignee, and `PUT /work-items/{id}/details`.
- Detail screen with layer and priority dropdowns and per-field saves.
- Verification caught a generated migration defaulting existing rows to
  priority `Low` instead of `Medium`; fixed before it left local dev.

## Milestone 10 — Authentication (2026-09-19)

- Users with hashed passwords, JWT access tokens, rotating refresh tokens,
  lockout after failed logins, authentication required by default, CORS
  narrowed to configured origins.
- Frontend login/registration, `AuthHttpClient`, session restore from a
  securely stored refresh token.
- Verification found and fixed: claim remapping (`MapInboundClaims`),
  int-serialised enums, a controller-level `[AllowAnonymous]` bug, logout
  broken by an unregistered plugin, and the fifo crash on
  `docker compose restart`.

## Milestone 8 — Search, filter and sort (2026-09-18)

- Client-side search over title and description, per-status column
  show/hide, and display-only sort (Manual, Title, Start date, Due date).
- Verified in a browser against seeded data.

## Milestone 7 — Time-frame filter (2026-09-18)

- Optional `StartDate` / `EndDate` on work items, set via `POST
/work-items/{id}/schedule`.
- A from/to overlap filter instead of a sprint system. Fixed a 1 px
  column overflow found while verifying.

## Milestone 6 — Hierarchy (2026-09-18)

- Drill into a card to make its children the lanes, breadcrumb
  navigation, reparent by dropping a card on another lane's label. No
  backend changes were needed.
- Verified end to end, including the new parent persisting across reload.

## Milestone 5 — API-connected board (2026-09-18)

- `ApiBoardRepository` replaced the fake in-memory repository; optimistic
  status moves with rollback; CORS enabled (later narrowed).

## Milestone 4 — Swimlane prototype (2026-09-18)

- Standalone board with fixture data, drag between status columns within
  a lane, cross-lane drops rejected. Verified with coordinate-based mouse
  simulation in a headless browser.

## Milestone 3 — Flutter shell (2026-09-18)

- App shell with the `ViewModel` base, `get_it` + `injectable` DI, and
  strict lint rules.

## Milestones 0–2 — Architecture, domain model, API (2026-09-18)

- ASP.NET Core solution (Domain / Infrastructure / Api), EF Core with
  PostgreSQL, the work-item tree with separate status and parent
  operations, cycle detection, explicit cascade delete, fractional rank,
  and operation-oriented REST endpoints with DTOs. Tests switched from
  xUnit to NUnit + Moq.
- Two-file Docker Compose setup (deployment vs. dev overlay), production
  and dev images, CI publishing to GHCR. Verified by running the
  production images together and calling the API through the nginx proxy.
