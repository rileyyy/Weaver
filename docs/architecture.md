# Architecture

How Weaver is built today and why. Each section records the decisions that
aren't obvious from the code alone, and the pitfalls behind them. The
design rules themselves are in [project_design.md](../project_design.md);
the history is in [CHANGELOG.md](../CHANGELOG.md). Milestones 16–20 are
planned in [the Spaces and agents spec](../.scratch/spaces-and-agents/spec.md)
and [adr/](adr/); sections describing something that plan replaces say so.

## Overview and layers

- **Backend:** ASP.NET Core 9, four projects under `backend/src/`:
  - `Weaver.Domain`: entities, enums, domain exceptions, `RankCalculator`,
    `TextValidation`. No dependencies on EF Core or ASP.NET.
  - `Weaver.Application`: the service interfaces and implementations,
    `IWeaverDbContext`, `IWorkItemHierarchy` and `JwtOptions`.
  - `Weaver.Infrastructure`: `WeaverDbContext`, EF configurations and
    migrations, the Postgres hierarchy queries and the JWT token service.
  - `Weaver.Api`: controllers, DTOs (`Contracts/`), MCP tools (`Mcp/`),
    `ApiExceptionMiddleware`, `Program.cs`.
- **Frontend:** Flutter (`frontend/lib/`), organised by feature (`auth`,
  `board`, `work_item_detail`), plus `core/` (infrastructure: DI,
  networking, dates, `ViewModel` base, theme) and `shared/` (what several
  features use: the `WorkItemStatus` and `User` models, cached lookup
  repositories, the `CurrentUser` interface and `WorkItemDetailOpener`).
- **Database:** PostgreSQL 16 through EF Core (Npgsql). The API applies
  pending migrations when it starts, unless `Database__MigrateOnStartup`
  is `false`; with a single backend instance there are no replicas to race
  ([ADR-0008](adr/0008-images-first-deployment.md)). A failed migration
  stops startup with one error naming the cause, rather than serving
  against an old schema. `dotnet Weaver.Api.dll migrate` applies them and
  exits, for deployments that want an explicit step. The decision is
  `StartupMigrationPolicy`, unit-tested without a database.
- **Deployment:** the published images. The example `compose.yaml` runs
  `db`, `backend`, `frontend` (nginx), `caddy` (TLS) and `backup`.

**Projects follow the layers.** `Weaver.Domain` has the entities and
domain exceptions. `Weaver.Application` has the service interfaces
(`IWorkItemService`, `ISpaceService`, …) and implementations, which query
through `IWeaverDbContext`: EF Core's `DbSet` API without the Npgsql
provider, model configuration or migrations. It is deliberately not a
generic repository; services compose EF queries directly.
`Weaver.Infrastructure` implements `IWeaverDbContext` with
`WeaverDbContext` and owns JWT signing. Each layer registers itself
(`AddWeaverApplication()`, `AddWeaverInfrastructure()`), so `Program.cs`
only wires the HTTP pipeline.

**Every controller depends only on a service interface**, never on
`WeaverDbContext`. This lets controller tests mock the service with Moq,
and it is what allows MCP tools to reuse the same business logic (see
[MCP](#mcp)).

## Domain model

### Work items and Spaces

- **Every work item belongs to exactly one Space**
  ([ADR-0001](adr/0001-spaces.md)). `WorkItem.SpaceId` is required. A
  top-level item is created in a chosen Space (`WorkItem.Create`), and a
  child always takes its parent's (`WorkItem.CreateChildOf`); a create
  that names a different Space for a child is a 400, not a move.
  Repeating-item copies are created in their template's Space.
- **Reparenting never crosses a Space.** `WorkItem.MoveToParent` (through
  `EnsureCanMoveUnder`) rejects a parent in another Space with
  `CrossSpaceParentException` (409), next to the "own parent" check; the
  service checks it before working out a rank. Making an item top-level
  keeps its Space.
- **Moving between Spaces is its own operation** (ticket 16),
  `WorkItemSpaceMoveService`. The root takes the target Space, parent and
  rank through `WorkItem.MoveToSpace`; each descendant follows its parent
  through `FollowParentToSpace`, parents first, so children keep sharing
  their parent's Space. Every status, Type and priority maps as the caller
  says or to a stated default (`SpaceValueMapper`): a status to the one
  with the same name, else the first of its category, else the first
  status; a Type or priority to its namesake, else cleared. The Type
  changes through `ChangeType`, so custom field values are cleared and
  recorded as on any change of Type. Sub-items in the Trash move too
  (`GetDescendantIdsIncludingTrashAsync`), so a restore never puts one in
  another Space from its parent. It's one save; a sub-item created under a
  moving item mid-move is the open race in ticket 48. The plan
  (`GET /work-items/{id}/space-move-plan`, `plan_work_item_space_move`)
  shows the defaults first; the dialog sends every mapping it showed.
- **"Top level" only means something within a Space.** Listing children
  (`GET /work-items`, `list_work_item_children`) and swimlanes
  (`GET /work-items/swimlanes`) takes a parent or scope item, or else a
  `spaceId` for that Space's top-level items; with neither it's a 400.
  `/work-items/all` and `list_all_work_items` take an optional `spaceId`.
- **Spaces are admin configuration.** Anyone signed in lists them
  (`GET /api/spaces`, `list_spaces`); only a human admin creates or
  renames one (`POST /api/spaces`, `PUT /api/spaces/{id}`) or sets its
  done-hiding period (`PUT /api/spaces/{id}/done-hiding-period`, see
  [Completion date](#completion-date-and-done-hiding)), and there's no
  MCP tool to change them. Names are unique ignoring case. There's no
  delete yet: nothing says what happens to a Space's items.
- **Each Space owns its statuses, priorities and Types** (tickets 12 and
  13). See [Lookup management](#lookup-management).
- **A board is scoped by an ancestor item, not by depth.** A board's
  swimlanes are the direct children of the scope item (or the Space's
  top-level items when there is none); its cards are each swimlane's
  direct children. Depth-from-root was rejected: hierarchy depth is
  unrestricted and branches can have different depths, so "level N"
  doesn't generalise. Drilling into a card on the frontend is just loading
  a different scope. The stored Board entity, which held only a name and a
  scope item, was removed with ticket 11; the Board is now a layout of a
  saved View (see [Views](#views)).
- **`ChangeStatus` and `Reparent` are separate operations**
  (`ChangeStatusAsync` / `ReparentAsync`, `PUT /work-items/{id}/status` /
  `/parent`), each touching only `StatusId` or `ParentId`. That a column
  drag can never reparent an item, and vice versa, is guaranteed by the
  method signatures, not by trusting callers to send one field to a shared
  update endpoint. The frontend mirrors this (see
  [Frontend](#frontend)).
- **Tree walks are single recursive queries.** `IWorkItemHierarchy`
  (Postgres: `PostgresWorkItemHierarchy`) answers "is the proposed parent
  this item or a descendant?" by walking up from the proposed parent, and
  lists a subtree's ids for cascade deletes, each with one recursive CTE
  instead of a query per level. The walk up uses Postgres's `CYCLE` clause:
  a tree that already contains a cycle fails loudly (a logged 500) instead
  of being reported as "no cycle". The EF InMemory service tests use an
  iterative test double with the same contract; the integration tests
  cover the SQL. The direction matters: walk up from the new parent, not
  down from the item.
- **Ranks load only their neighbours.** Placing an item reads the anchor's
  rank and the next one up; the whole cell is loaded only when two
  siblings share a rank and need respacing.
- **Deleting an item with children requires `cascade: true`**, otherwise
  `WorkItemHasChildrenException` (409). There is no silent subtree loss.
- **Delete moves to the Trash** ([ADR-0007](adr/0007-trash.md),
  `WorkItemTrashService`, ticket 19). The item and its live subtree get
  `TrashedAtUtc` and `TrashRootId` (the item the delete was made on), and
  every row stays, with its links, comments and history.
  - **An EF global query filter** (`TrashedAtUtc == null`) hides trashed
    items from every normal read, so a new query can't forget to. Comments,
    links (either end trashed) and recurrences carry matching filters. EF
    applies the filter inside navigations too, but `IgnoreQueryFilters()`
    lifts it from them as well, so a projection that must skip the Trash
    whatever the caller (the summary's child count) filters explicitly. Raw SQL skips EF's filters, so the recursive descendant walk
    repeats it; the walks up don't need it, since a live item never sits
    under a trashed one.
  - **Opting out is explicit**, with `IgnoreQueryFilters()`: the Trash
    itself, the query's include-deleted input, "changes since T", history
    value names, the recurrence duplicate check, and admin changes that
    re-point every item using a value (deleting a status, Type, priority or
    field, deactivating a user), so a restore never brings back an item
    pointing at something gone.
  - **`FindAsync` returns an already-tracked entity even when the filter
    would hide it**, so the Trash service looks items up with a query.
  - **Restore only takes the item a delete was made on**, and brings back
    exactly the items with its `TrashRootId`: a child deleted separately
    earlier stays in the Trash. It goes under its parent if that's live,
    else the nearest live ancestor (at the end of that cell), else the top
    of its Space. Restoring an item deleted with its ancestor is a 400
    naming the ancestor.
  - **Repeating items:** a trashed copy still counts as generated, so it's
    never recreated (its unique source/date pair would refuse a second copy
    anyway). A trashed template's recurrence is hidden, so generation skips
    it; restoring it restarts generation from today rather than backfilling
    the time it spent in the Trash.
  - **Who deleted it** comes from the latest `deleted` history entry, not a
    column: an account with history can't be hard-deleted, so it's always
    there to name.
  - **Purge works per delete**, like restore, and only humans can do it:
    `WorkItemPurgeService` checks the caller's account kind, since a
    read-write agent passes every write check. Both `WorkItemLink` FKs and
    the parent FK are `Restrict`, so purge removes links itself (recording
    a `link` removal on the surviving side) and detaches trashed children
    of other deletes, which stay restorable as roots. Rows go through
    `IAttachmentFileCleanup`, so files are deleted only after the rows
    commit. Each purged item records `purged`; its history is kept.
  - **Retention** is a `ServerSettings` column (30 days by default),
    counted as elapsed time since the delete. `TrashPurgeWorker` purges
    expired deletes hourly as the system actor.
- **Rank is a fractional `double` scoped to `(SpaceId, ParentId, StatusId)`**
  (the Space so each Space's top-level items rank among themselves), not
  globally unique. Drag-and-drop can insert between two siblings without
  rewriting anyone else's rank (`RankCalculator`). There is no rebalancing
  job; when two siblings share a rank (concurrent inserts at the same
  spot), placing next to them respaces that cell
  (`RankCalculator.EvenlySpaced`).
- **`Number` is a Postgres identity column** (`GENERATED ALWAYS AS
  IDENTITY`, unique), set only by the database and used for display
  (`#N`). `Id` (a GUID) remains the key everywhere else. The InMemory
  provider doesn't generate it, so only the integration tests cover it.

### Types, priority and other fields

Types replaced Layers in ticket 13 and gain custom fields in ticket 14
([ADR-0002](adr/0002-types-and-custom-fields.md)).

- **`WorkItemType` is a lookup table of one Space** (`Id`, `SpaceId`,
  `Name`, `Order`, `Color`); each Space starts with Project / Goal / Task
  in the colours the Layers had. The colour marks a Type's items on the
  board (a card's left bar, the Hierarchy title's underline).
  `WorkItem.TypeId` is nullable and deliberately **not validated against
  the parent/child tree**: the Type is a label, not a constraint, so any
  Type can be nested under any other (a Task can parent a Project).
- **The rename from Layer kept the data.** `RenameLayersToTypes` is written
  by hand as table, column, key and index renames (the generated one
  dropped the table), and renames history entries' `layerId` field to
  `typeId`, so old entries show as Type. `PerSpaceTypes` gives every Space
  a copy of each global Type and moves items, and the Type history of items
  that still exist, onto the copy in their own Space by name. Unlike
  statuses, it needs no reset, because an item may have no Type. Older
  migrations keep the name Layer.
- **`WorkItemPriority` is a lookup table too** (`Id`, `SpaceId`, `Name`,
  `Order` from least to most urgent); each Space starts with Low / Medium /
  High / Urgent. It
  replaced a fixed enum so priorities can be renamed, reordered or added
  without a schema change. `WorkItem.PriorityId` is nullable like
  `TypeId`, and a new item has no priority unless one is given; the enum
  defaulted to Medium only because an enum column needs a value. The
  `ConfigurablePriorities` migration maps each stored enum ordinal onto the
  seeded row with that `Order`, and its `Down` maps back (anything else
  becomes Medium). Clients read a Space's list from `GET
  /work-item-priorities?spaceId=` / `list_work_item_priorities`; admins edit them
  like Types and statuses (see [Lookup management](#lookup-management)).
- **Fields that were requested but map onto existing ones:**
  - There is no `DueDate`: `EndDate` is the due date (the board's sort
    option labels it "Due date").
  - There is no stored `ProjectId`: "which project is this under" is the
    nearest ancestor whose Type is Project. Storing it would duplicate the
    hierarchy and drift on reparent. Spaces are the planned top-level
    container ([ADR-0001](adr/0001-spaces.md)).
  - `CreatedAtUtc` / `UpdatedAtUtc` are the created/updated timestamps.
- **Statuses** start as To Do, Doing, Done in each Space, with a per-row
  `Color`, so colours can be changed without a schema change.

### Custom fields

Each Type defines its own fields (ticket 14,
[ADR-0002](adr/0002-types-and-custom-fields.md)).

- **Definitions are a table per Type** (`CustomFields`: `TypeId`, `Name`,
  `Order`, `Kind`, `Options` as jsonb), built on `LookupValue` and
  `LookupService<T>` with the Type as the siblings' scope: names unique and
  order kept within the Type, every change by a human admin. They cascade
  with their Type. The kinds are text, Markdown, number, currency, date,
  checkbox, single select, multi-select, user and URL. **A field's kind is
  fixed once created**, since stored values have its shape; to change it,
  remove the field and add another.
- **Select options carry ids** (`CustomFieldOption`), and values store the
  id, so renaming an option keeps every choice of it. An edit sends the
  whole option list in order; an option left out is removed.
- **Values are one jsonb document per item** (`WorkItems.CustomFields`),
  keyed by field id, so adding or removing a field needs no migration.
  `CustomFieldDefinition.Normalize` turns what a caller sent into the one
  stored shape (trimmed text, numbers without trailing zeros, currency with
  at most 2 decimals, `yyyy-MM-dd` dates, http(s) URLs, option ids in option
  order; blank text, an empty multi-select and `null` clear the field), so
  equal values never count as a change. Values are compared re-serialized,
  because Postgres hands jsonb back with its own whitespace.
- **Only a validated `CustomFieldPatch` can be applied.**
  `WorkItemService.SetCustomFieldsAsync` loads the item's Type's
  definitions, validates the patch (an unknown field, the wrong kind or an
  unknown option is a 400), checks that each user named exists and is
  active (as for the assignee), then applies it with `expectedVersion`. A
  field left out keeps its value.
- **Changes go by `fields.<fieldId>`**, in `changedFields` and in history.
  Built-in names have no dot, so they can't collide, and a rename doesn't
  change old entries. History views add `fieldName` (the field's current
  name) and name a user or option value.
- **Changing an item's Type clears its values**, since another Type's
  fields are other fields. Each cleared value is recorded in history (old
  value, new `null`) and reported as changed. That holds for a detail edit
  and for an admin deleting the Type with a replacement. Carrying values to
  a same-named field of the new Type was left out: it would be a guess.
- **Removing a field removes its values** from every item of the Type, and
  removing a select option removes that choice; each item records it in
  history, attributed to the admin.
- **Endpoints:** `GET /api/custom-fields?typeId=`, `POST` with a `typeId`,
  `PUT /{id}` (name and option list), `PUT /order?typeId=`, `DELETE /{id}`;
  `PATCH /api/work-items/{id}/fields` with `values` by field id. Work items
  carry their values in `fields`. Over MCP `list_work_item_types` returns
  each Type with its fields and `set_work_item_fields` sets values; no tool
  changes a definition, and a test keeps it so.

### Roll-ups

A number or currency field can roll up (ticket 15,
[ADR-0002](adr/0002-types-and-custom-fields.md) and its amendment).

- **The flag is `CustomFields.RollsUp`**, set by an admin when adding or
  editing the field. The domain refuses it on any other kind. The field
  update takes it as a required argument: a `PUT` replaces the definition,
  so leaving it out turns the roll-up off.
- **Totals are summed when read, never stored** (`RollUp.Sum`): each field
  on its own, by id, over every live descendant at any depth, through items
  of any Type. The item's own value isn't counted; it's shown beside the
  total. Because a currency field fixes one currency and fields are never
  summed together, a total can't mix currencies.
- **`WorkItemRollUpService`** finds the descendants with
  `IWorkItemHierarchy.GetDescendantIdsAsync` (one recursive query that
  already leaves trashed subtrees out), then loads them and their Types'
  roll-up fields. The item's own Type's fields come first, then the other
  Types' in Type order.
- **Endpoints:** `GET /api/work-items/{id}/roll-ups` (one total per field
  with a value, with its name, Type and kind) and
  `GET /api/custom-fields?spaceId=` (every Type's fields in a Space, Types in
  their order).
  Over MCP, `get_work_item` returns a `WorkItemDetailDto`: the item plus
  `rollUps`. List and search results don't carry them,
  since that would walk every listed item's subtree.
- **The detail dialog** loads the roll-ups with the item and again when a
  sub-item's dialog closes. A total of one of the item's own fields shows
  under that field's editor; totals of fields only other Types have get a
  "Totals of sub-items" section, named with their Type.
- **The Hierarchy sums its own tree** (`HierarchyRollUps`), from the items
  in the shared store and the Space's fields, rather than asking the server
  per row. It holds the whole Space, so the totals match the server's, and
  they follow every edit without a reload. Dart adds doubles where the
  server adds decimals, so each total is rounded to the most decimals any
  of the field's values has (0.1 + 0.2 shows 0.3). The rule is written
  twice, in C# and Dart, so a change to it needs both. A Totals column, shown only when
  the Space has a field that rolls up, gives each row's own value and its
  sub-items' total.

### Lookup management

- **Statuses, Types and priorities share `LookupValue`** (`Id`, `Name`,
  `Order`), and their services share `LookupService<T>`: list, append,
  edit, reorder, delete. Every change needs an admin. Names are required,
  trimmed, at most 100 characters and unique case-insensitively (409),
  since two same-named values couldn't be told apart in any picker. A
  status's or Type's colour must be `#RRGGBB` and is stored upper-case
  (`HexColor.Normalize`).
- **Every lookup is owned by a Space** (`SpaceLookupValue`,
  `SpaceLookupService<T>`, [ADR-0001](adr/0001-spaces.md)), and every rule
  works within a value's siblings (`SiblingsOf`), the other values of its
  Space. Names, order (a unique `(SpaceId, Order)` index), replacements and
  the last-status rule all apply within one Space: "Done" can exist in
  every Space, a replacement from another Space is a 404, and each Space
  keeps at least one status.
- **A new Space starts with defaults** (`SpaceDefaults`): To Do / Doing /
  Done, Low / Medium / High / Urgent and Project / Goal / Task, added by
  `SpaceService.CreateAsync`.
  Nothing is seeded globally any more. The `PerSpaceStatusesAndPriorities`
  migration drops the old global rows and gives every existing Space the
  defaults; like `AddSpaces`, it fails on a database that already has work
  items, which must be reset (nothing is deployed yet).
- **A work item only takes its own Space's status, priority and Type.** Creating,
  moving or editing an item with another Space's is a 400
  (`LookupNotInSpaceException`), and repeat occurrences start in the first
  status of the template's own Space.
- **Endpoints:** `GET /api/statuses?spaceId=` (and
  `/work-item-priorities`, `/work-item-types`; the Space is required);
  `POST` with a `spaceId` in the body; `PUT /{id}` (statuses and Types take
  a `color` alongside the `name`); `PUT /order?spaceId=` with every id in
  its new order; `DELETE /{id}?replacementId=`. DTOs carry `spaceId`. Over
  MCP agents can only read the lists (`list_statuses`,
  `list_work_item_priorities` and `list_work_item_types` take a
  `spaceId`), never change them
  ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md)).
- **Deleting moves the items, it doesn't orphan them.** Work items using
  the deleted value move to `replacementId`. Items may go without a Type
  or priority, so omitting it clears them. Every item needs a status, so a
  status still in use without a replacement is a 409
  (`LookupValueInUseException`), and the last status can't be deleted.
  Moved cards are appended after the last rank in each lane's replacement
  column, keeping their relative order: no existing card's rank changes and
  nothing is reparented.
- **Reordering parks values on negative orders first.** `Order` has a
  unique index and Postgres checks it row by row, so writing the final
  positions directly fails as soon as two values swap. `ReorderAsync`
  writes `-1, -2, …`, then the real orders, in one transaction
  (`IWeaverDbContext.BeginTransactionAsync`). InMemory has no transactions,
  so the swap is covered by an integration test against Postgres. Deletes
  leave gaps in `Order`; only the relative order matters.

### Tags

- **`Tags` is a `List<string>` mapped to a Postgres `text[]` column**, the
  first scalar-list field on `WorkItem`. No join table and no index: the
  [work-item query](#work-item-query) matches tags lower-cased inside the
  array (`unnest`), which an index on the raw array couldn't serve, and
  the item count is household-sized. Tags stay global across Spaces.
- **Tags are set as a whole** (`SetTagsAsync`, `PATCH
  /work-items/{id}/tags`); there is no add/remove-one endpoint. The client
  sends the complete list, or `null` to clear it; leaving `tags` out
  changes nothing.
- **Normalisation is server-side:** each tag is trimmed, blank tags are
  rejected (`InvalidWorkItemTagException`, 400), and duplicates are removed
  case-insensitively with the first casing kept. At most
  `WorkItem.MaxTags` (20, counted after de-duplication) tags of at most
  `WorkItem.TagMaxLength` (50) characters. `text[]` has no limit of its
  own, so without these there was no bound.

### Comments and links

- **A link is one typed row, read from either side.** `WorkItemLink`
  stores `WorkItemId` / `LinkedWorkItemId` and a `WorkItemLinkType`
  (`Related`, `Blocks`, `Duplicates`); `Blocks` and `Duplicates` read
  "`WorkItemId` blocks/duplicates `LinkedWorkItemId`". Callers speak in a
  `WorkItemLinkRelation` seen from one item (adding `BlockedBy` and
  `DuplicatedBy`): creating "A blocked by B" stores "B blocks A", and
  `ListForWorkItemAsync` matches either column and reports the other side
  and the relation from the requesting item, so a link created from A is
  listed from B with the same id
  ([ADR-0009](adr/0009-typed-links-and-dependency-flags.md)).
- Two items have at most one link, of any type, in either direction
  (`DuplicateWorkItemLinkException`, 409). A blocks link against one
  already running the other way is a cycle (`BlockingCycleException`, 409,
  checked first so the message says why); a self-link is
  `SelfWorkItemLinkException` (400). Links can cross Spaces. Longer cycles
  (A blocks B blocks C blocks A) aren't checked.
- **A URL link** (`WorkItemUrlLink`) belongs to one item: an absolute
  http(s) `Url` and an optional `Label`. It cascades with its item like a
  comment, so purging the item removes it; typed links are `Restrict` on
  both ends and purge removes them itself.
- **`whenResolved` and `blockedBy` come from live blockers** (ADR-0003,
  ADR-0009), which are other rows, so they're computed where items are
  mapped to `WorkItemDto`: `WorkItemDtoMapper` asks
  `IWorkItemBlockers.BlockersOfAsync` once per response, for every item.
  `blockedBy` lists each live blocker (Done ones too, so the Roadmap can
  still draw their arrows) with its id, number, title and whether that
  link is a dependency conflict; it's `[]`, never null, when
  nothing blocks the item. An item is blocked for `whenResolved` when the
  list isn't empty. A trashed blocker stops counting without the Trash
  having to update anything: the links' Trash filter hides it, and
  `BlockedWorkItems.LiveBlocksLinks` checks the blocker's Trash state too,
  since a query that reads trashed rows lifts that filter. The work-item
  query's `UnresolvedWhenOnly` uses the same blocked-ids query and
  `WorkItem.WhenResolutionNeedsBlockersRule`, so
  `list_unresolved_when_items` and `whenResolved` can't disagree.
- **A dependency conflict** (ADR-0009) is a blocked item whose `StartDate`
  is before its live blocker's `EndDate`. Starting on the blocker's end
  date is not a conflict (a blocker can finish and the next item begin
  the same day), nor is a link missing either date, nor one where either
  item is Done (read from `CompletionDate`, which is set exactly while
  Done, to save joining Statuses). The rule is written once, in
  `BlockedWorkItems.LiveBlocksLinkRows`, which both `blockedBy` and
  `IDependencyConflicts` (the MCP `list_dependency_conflicts` tool) read,
  so the badge and the list can't disagree. Weaver never changes dates in
  response to a conflict. Known gap: changing a blocker's dates, Done
  state, Trash state or links changes whether the *blocked* item
  conflicts, but not the blocked item's version or history, so
  changes-since-T doesn't report it (the same gap `whenResolved` has).
  The client that made the change refreshes the affected items itself
  (`BlockedItemRefresher`); other clients catch up on their next load.
- Comments cascade-delete with their work item; editing is restricted to
  the author (see [Authentication](#authentication)).

### Change history

- **Every change to a work item appends a `WorkItemHistoryEntry`**
  ([ADR-0005](adr/0005-change-history.md)): create, every field change,
  status, parent, comments, links, repetition and delete. It records when,
  the account, the API key (agents), the actor label, the run id (when an
  agent sent one: free text, at most 100 characters, like the label), the
  field, and the old and new value as JSON in the shape the field has in the API (`jsonb`).
  Field changes use `WorkItemField` names; the rest use
  `WorkItemHistoryField` (`created`, `deleted`, `comment`, `link`,
  `recurrence`), which documents what each one's values hold.
- **The Application services write it**, through `WorkItemHistoryRecorder`,
  so REST and MCP can't diverge. Entries are added to the context before
  `SaveChanges`, so they commit with the change or not at all: a failed or
  rejected write records nothing. The domain write methods return
  `FieldChange`s (old and new value, from `ChangedFields`), so services
  don't diff; a write that changes nothing records nothing.
- **Creating an item also records its Space** (`spaceId`, old value
  null), via `WorkItemHistoryRecorder.RecordCreation`, so its history
  says where it started even after a move between Spaces (ticket 16).
  Like the other id fields, readers get the Space's name.
- **A Space's own history is separate.** Creating and renaming a Space
  append a `SpaceHistoryEntry` (`created`, `name`), with the same who and
  when, since `WorkItemHistory` is keyed by a work item. Nothing reads it
  yet (ticket 47). Its account and key FKs are `Restrict` like work item history's, so
  an admin who created a Space can't be hard-deleted either.
- **Rank isn't recorded.** It's a card's position in its board cell, which
  every drag rewrites; a reorder alone records nothing.
- **Changes made as a side effect record too**, attributed to whoever
  caused them: deleting a status, Type or priority moves its items;
  deactivating or deleting a user unassigns theirs; deleting an item
  removes the links on the items outside the subtree; setting a repetition
  creates occurrences. Occurrences created by `RecurrenceGenerationWorker`
  have no caller and record as `HistoryActor.System` (no account).
- **Append-only.** Nothing updates or deletes an entry, and there is no
  endpoint or tool that could. `GET /api/work-items/{id}/history` lists an
  item's entries newest first, with the account's name and kind, the key's
  label, and names for id values (status, Type, priority, user, the other
  item of a parent or link), looked up at read time so a renamed status
  shows its current name and a deleted one falls back to the raw id.
- **Entries outlive their item**: `WorkItemId` has no foreign key, so a
  deleted item's history (ending in `deleted`) still reads, and "changes
  since T" sees deletes. `AccountId` and `ApiKeyId` are
  Restrict foreign keys, so an account that has changed any work item
  can't be hard-deleted (409 `UserHasHistoryException`; deactivate it
  instead, as for comment authors). That also keeps an agent's keys, which
  are cascaded away only with an agent that has no history.
- **Ordering** is by the identity `Id`, not the timestamp, since several
  entries saved together share one `CreatedAtUtc`.
- **"Changes since T"** (`GET /api/work-items/changes`, MCP
  `list_changes_since`, `WorkItemChangesService`) lists the work items
  with entries at or after `since`, each with those entries oldest first,
  as a `WorkItemSummary` (number, title, Space, parent, status, Type,
  dates, assignee and child count, by name):
  - **Pages by each item's first entry since T**, and the opaque cursor
    (`WorkItemChangesCursor`) is that entry's `Id`. An item's first entry
    never changes, so changes landing between pages don't reorder or
    repeat items: an item already returned isn't shown again, and a newly
    changed one comes at the end. Caveat: identity values are handed out
    at insert, not commit, so a transaction that commits after a page past
    its `Id` was read can be skipped. That's acceptable for a handful of
    writers; ask again from an earlier T if it matters.
  - **Deletes and restores are marked.** `deleted: true` means the item is
    deleted now; a trashed one keeps its row, so its summary is read from
    it. A purged item has no row, so its summary (title, last Space) is
    rebuilt from its history (`DeletedItemTrace`), with the number and the
    other fields null. `restored: true` means it came back from the Trash
    since T and is live now; at most one of the two is set, however often
    it went back and forth.
  - **The Space filter** uses an item's row's Space, trashed or not, or
    for a purged item the Space its history last gave it. An item created before
    history recorded the Space (between tickets 10 and 11) and then
    deleted matches no Space.
  - **Excluding the caller's own changes** filters by account, and keeps
    Weaver's own entries (null `AccountId`) explicitly.
  - `since` is normalised to UTC: Npgsql refuses any other offset.
  - **Restore isn't in it yet**: Trash (ticket 19) adds a `restored`
    marker, and `deleted` then comes from the trash state rather than
    from the row being gone.
  - Over MCP, nulls are left out like everywhere else in tool results,
    except an entry's `oldValue`/`newValue`: a null there is what the
    entry records, so it's always written.
- **Anything new that changes a work item must record history**: a new
  field, a new operation, Spaces and Types, custom fields, When, Trash
  restore, attachments. Return `FieldChange`s from the domain method and
  pass them to the recorder in the service, and give every new MCP write
  tool `actorLabel` and `runId` arguments (an integration test fails a
  write tool without either).

### Schedule dates

- **`StartDate` / `EndDate` are calendar dates** (`DateOnly`, Postgres
  `date`), sent as `yyyy-MM-dd`, each optional. They were originally
  `DateTimeOffset`, and the client sent the picked day as local midnight
  converted to UTC, so east of UTC the stored value was the previous day.
  A day on a schedule is not an instant; storing it as one invites
  time-zone bugs.
- **They're set by their own operation**, `RescheduleAsync` (`PATCH
  /work-items/{id}/schedule`), separate from status, parent and create.
  A date left out is kept, so the only rule (start not after end, when
  both are set) is checked in `WorkItem.Reschedule` against the dates the
  item ends up with.
- **An item with no dates at all is unscheduled**: it's in the Backlog
  unless it's Done, and matches no date window
  ([ADR-0003](adr/0003-when-and-backlog.md)). An item with only one of
  the two dates is open on the other side.

### Completion date and done-hiding

- **`WorkItem.CompletionDate` is the calendar date the item entered a
  Done-category status** (`DateOnly`, `completionDate` in every work item
  DTO and summary), taken from `IServerCalendar.TodayAsync` so a
  late-evening finish isn't dated tomorrow in UTC. It's null while the
  status isn't Done, and kept when the item moves between two Done
  statuses, so it says when the work was finished.
- **One rule, applied wherever the status or its meaning changes**: a
  private `WorkItem.FollowStatusCategory` that create, `MoveToStatus`, the
  Space move (`TakeSpaceValues`), repeating copies and
  `FollowStatusRecategorized` all go through. Domain methods take the
  `Status` entity and today's date, so the domain needs no clock or
  lookups. Repeating copies never inherit the template's date. An admin
  moving a status into or out of Done (`StatusService.UpdateAsync`)
  completes or reopens every item in it, trashed ones too, each recorded
  in history; so does deleting a status into a replacement of the other
  kind.
- **Each Space has a done-hiding period**, `Space.DoneHidingDays`
  (default 14, 1 to 3650), set by an admin and recorded in the Space's
  history (`doneHidingDays`). Agents read it from `list_spaces`.
- **The query's `ApplyDoneHiding`** leaves out Done items whose completion
  date is before today minus their own Space's period (one query can
  span Spaces). An item completed exactly that many days ago is still
  listed, and so is a Done item with no completion date. Items already
  Done when the column was added were backfilled with the date of their
  last update.

### Repeating work items

- **The schedule lives in its own table, the template stays a normal
  item.** `WorkItemRecurrence` is keyed by the work item's id (at most one
  schedule per item) and cascades with it. The item itself is the template:
  generation reads its current fields and parent each time and never
  changes it. Generated copies are ordinary work items, so they get every
  board feature for free and remain for reference after they're done.
- **Schedule maths is a pure domain value object**, `RecurrenceSchedule`.
  Weekly/Bi-weekly use a `RecurrenceDays` bitmask (Monday first; not
  `DayOfWeek`'s Sunday-zero ordinals). Bi-weekly counts weeks from the
  Monday of the start date's week. Monthly/Quarterly/Yearly fall on the
  start date's day of the month and ignore weekdays (normalised to none on
  save); each date is computed as an offset from the start date, not the
  previous occurrence, so a clamped 31st never drifts to the 30th forever.
- **Copies record where they came from.** `WorkItem.RecurrenceSourceId`
  (`SetNull` when the template is deleted) and `RecurrenceDate` have a
  unique index together. That index is the backstop against creating an
  occurrence twice; hand-made items have both null, and Postgres treats
  nulls as distinct.
- **`GeneratedThrough` is a watermark.** Generation only considers dates
  after it (or from today when unset), so deleting a generated copy doesn't
  bring it back on the next run. Changing the schedule clears it, so the
  new schedule's dates within the window appear straight away; dates that
  already have a copy are skipped. The downside: a copy deleted inside the
  current window reappears if the schedule is then edited.
- **No backfill, but catch-up.** A new schedule starts from today even if
  its start date is in the past. If the server was down longer than the
  lead window, the next run creates the missed occurrences, because they
  were real tasks that fell due.
- **The template's own start date counts as an occurrence.** Setting a
  task dated Monday to repeat every Monday doesn't immediately create a
  second copy for that Monday.
- **A generated copy can't be given its own schedule** (400). Otherwise
  every copy would spawn its own series. Sub-items that are themselves
  copies of a repeating sub-item aren't copied into the parent's
  occurrences either.
- **Generation runs in `RecurrenceGenerationWorker`** (a `BackgroundService`
  in `Weaver.Api/Background`) at startup and hourly, and synchronously when
  a schedule is saved. It catches everything: an exception escaping a
  `BackgroundService` stops the whole host. `GenerateDueAsync` isolates
  failures per schedule. Each occurrence is saved on its own because EF
  orders one batch's inserts by primary key, which with random GUIDs gave
  display numbers out of date order.
- **"Today" is the date in the server time zone**, from `IServerCalendar`
  (the admin-set IANA zone in `ServerSettings`, applied to the injected
  `TimeProvider`, so the lead window is testable). It's read once per
  operation, so one generation run can't straddle two "todays". Anything
  else that compares dates with the present should ask the same calendar.

## API

- **Operation-oriented endpoints** for state changes:
  `/work-items/{id}/status`, `/parent`, `/schedule`, `/assignee`, `/tags`,
  and `/details` for title, description, Type, priority and When
  together. Those share an endpoint because none of them
  carries the cross-cutting risk that keeps status and parent apart.
- **When quick picks are resolved on the server.** `GET /quick-picks`
  returns this weekend, next week (Monday to Sunday) and next month as
  dates for today from `IServerCalendar`, via `QuickPickDates` in
  `Weaver.Domain`. A client applies one with an ordinary `PATCH
  /schedule`, so there's no second way to set dates. No range starts in
  the past: on a Sunday "this weekend" is just that Sunday.
- **`whenResolved` is computed, not stored**: true when the item has a
  When and at least one date or a live blocker
  (`WorkItem.IsWhenResolved`). Clients and agents read it rather than
  re-deriving the rule.
- **DTOs only** (`Weaver.Api/Contracts`); EF entities are never
  serialised.
- **Verbs follow the resource.** `POST` creates and answers `201` with a
  `Location` (work items, Spaces, comments; a link's `Location` is its
  work item's link list, since a link has no view of its own). Patching
  a group of work item fields (`/details`, `/schedule`, `/tags`) is a
  `PATCH` to that sub-resource; replacing a single value (`/status`,
  `/parent`, `/assignee`) is a `PUT`. Controllers
  declare their `201`/`204` and problem-details error responses, so the
  OpenAPI document shows the error shapes.
- **Enums serialise as their string name** (`"ToDo"`, `"Human"`) via a
  global `JsonStringEnumConverter` in `Program.cs`. Without it the API
  sent ints, and the frontend's `as String` cast failed on a response that
  had actually succeeded, surfacing as a misleading "could not create
  account". MCP needs the same setting separately (see [MCP](#mcp)).
- **Errors map to status codes by kind.** Every expected failure is a
  `DomainException` carrying a `DomainErrorKind` (NotFound → 404,
  Validation → 400, Conflict → 409, Unauthorized → 401, Forbidden → 403).
  `DomainErrorStatusCodes` is the only mapping; `ApiExceptionMiddleware`
  writes it as `application/problem+json`, and `McpExceptionTranslation`
  turns any `DomainException` into a tool error with the same message. A
  new exception needs no transport change. Postgres unique violations are
  translated to `UniqueConstraintViolationException` (409) inside
  `WeaverDbContext`, so nothing above Infrastructure sees provider error
  codes.
- **Rules live on the entities.** Entities have private setters and
  change state through methods (`WorkItem.Reschedule`, `SetTags`,
  `MoveToStatus`, `Comment.Edit`, `User.RecordFailedLogin`, …), so every
  caller gets the same checks and the rules are plain unit tests in
  `Weaver.Domain.Tests`. Services keep the rules that need other rows
  (cycle detection, existence checks, rank neighbours) and orchestrate
  loading and saving. Field validation uses `TextValidation` in
  `Weaver.Domain`, so REST and MCP share it. A broken rule throws
  `DomainValidationException` (400 in REST, a tool error in MCP). Use it
  for new field rules instead of adding exception types. Max lengths are
  constants on the entities (`WorkItem.TitleMaxLength`,
  `Comment.BodyMaxLength`, `Space.NameMaxLength`) that the EF
  configurations also use, so the validation limit and the column limit
  can't drift. Before this, oversized input reached the database and
  returned a 500.

### Work-item query

One query in the Application layer (`IWorkItemQueryService`,
[ADR-0006](adr/0006-server-side-query-and-views.md)) serves `GET
/api/work-items/search` (full `WorkItemDto`s, for Flutter from ticket 18)
and MCP `search_work_items` (`WorkItemSummary`s). Views store its inputs
(see [Views](#views)).

- **Inputs** (`WorkItemQuery`): Spaces, text, `number`, status
  categories, assignee, tags with `tagMatch` `Any` or `All`, Types, a
  `from`/`to` date window, `backlogOnly`, `applyDoneHiding` (see
  [Completion date](#completion-date-and-done-hiding)), `updatedSince`
  and `includeDeleted`. Each input
  left out applies no filter, the ones given AND together, and a list
  input matches any of its values. Over REST a list repeats its key
  (`?tags=a&tags=b`).
- **`#123` resolves directly**: text that is just `#` and digits looks up
  that Number instead of searching.
- **Text search is Postgres full-text search** with the `simple`
  configuration over title, description and When. Every typed word must
  match as a word or the start of one (`paint:* & hall:*`), built by
  `PostgresPrefixQuery`, which keeps only letters and digits so typed
  text can't break `to_tsquery`. `simple` rather than a language because
  items mix languages and prefix matching does the work stemming would.
  A GIN expression index (`IX_WorkItems_TextSearch`, Npgsql's
  `IsTsVectorExpressionIndex`) serves it. Postgres only uses an expression
  index when the query's expression is identical, so
  `PostgresWorkItemSearch` is written to translate to the index's exact
  `to_tsvector(...)`, and a unit test reads the SQL to keep them in step.
  Tags aren't searched by text; the tag filter covers them.
- **The date window follows [ADR-0003](adr/0003-when-and-backlog.md)**:
  an item with a date matches when its dates overlap the window (a
  missing start or end, on the item or the window, is open on that side),
  and an undated item matches only when one of its descendants matches
  the whole query, so a layout can still draw the parent a listed item
  sits under, and never one with nothing listed beneath it. A dated
  parent is judged by its own dates. The undated ancestors come from
  `IWorkItemHierarchy.GetSelfAndAncestorIdsAsync`, one recursive query
  over the parents of the dated matches. A window ending before it
  starts is a 400.
- **Backlog-only** is every non-Done item with no dates.
- **Scheduled-or-in-progress-only** is Overview's rule: every item in a
  Doing-category status, whatever its dates, or with dates overlapping the
  window (any dated item when there's no window). It's an OR, which the
  other inputs can't express, so it lives inside the date-window rule:
  undated ancestors of what it lists come along as for a window, and a
  window narrows only the dated items, never the Doing ones.
- **Pages by `Number`**, with an opaque cursor (`WorkItemQueryCursor`)
  holding the last Number on the page. A Number never changes and new
  items get higher ones, so items moving between pages (a rank change) or
  being added can't make a reader skip or repeat one, which an offset over
  `Rank` could. An item that stops matching between pages simply isn't
  returned. Default 50 per page, at most 1000. Cursors share
  `OpaqueCursor`'s encoding with "changes since T" but carry their own
  prefix, so one listing refuses the other's cursor.
- **Provider-only matching sits behind `IWorkItemSearch`** (full-text and
  tags, `PostgresWorkItemSearch`), like `IWorkItemHierarchy`, because the
  InMemory provider used by the service tests can't run either. The
  tests use `InMemoryWorkItemSearch`; the Postgres versions are covered by
  `WorkItemSearchTests` in the integration tests, including an `EXPLAIN`
  with sequential scans off that shows the index can be used.
- **Done-hiding runs before the date window**, like the other filters,
  so a hidden Done item can't bring its undated parent into a window.

### Views

A View is a stored instance of the work-item query plus a layout
([ADR-0006](adr/0006-server-side-query-and-views.md), ticket 23).

- **What a View stores** (`View`, `ViewDefinition`): a name, its Spaces
  (`uuid[]`, empty for every Space, including Spaces created later), text,
  tags (`text[]`, normalised by `TagList` like an item's), tag match,
  done-hiding, scheduled-or-in-progress-only and a layout (Board,
  Hierarchy or Roadmap; Agenda comes with ticket 24). **No time range**:
  saved dates go stale within a week, so the window is chosen when a View
  is shown (the header's range, the Roadmap's timeline) or run
  (`run_view`'s `from`/`to`). Hidden status columns and sort aren't
  stored either; they're layout, not query inputs.
- **Views are shared.** Anyone signed in lists them, and anyone who may
  write saves, replaces (`PUT` takes the whole definition, the name
  included) or deletes one. Names are unique ignoring case, as for
  Spaces.
- **Overview is built in, not stored** (`View.Overview()`, id
  `View.OverviewId`): every Space, scheduled-or-in-progress-only, old Done
  items hidden, Board layout. It's listed first, read and run like any
  View, and changing or deleting it is a 400 (`BuiltInViewException`).
  Its name counts as taken.
- **Each user's default View** is a `DefaultViews` row (user id as key,
  View id), not a column on `User`, which stays about the account. No row
  means Overview, so picking Overview removes the row. Both foreign keys
  cascade: deleting a View sends whoever had it back to Overview.
- **Endpoints:** `GET /api/views`, `GET/PUT/DELETE /api/views/{id}`,
  `POST /api/views`, `GET/PUT /api/views/default` (`{ viewId }`). Over MCP
  agents only read them: `list_views` and `run_view` (summaries, paged
  like `search_work_items`). Views are the humans' layouts; anything a
  View asks, an agent can ask with `search_work_items`.

### Optimistic concurrency

- **`WorkItem.Version` maps to Postgres's `xmin`** as an EF concurrency
  token and is returned as `Version` on every `WorkItemDto`.
- On its own the token only guards the moment between a request's read and
  its write. So **every work-item write** (details, schedule, tags, status,
  parent, assignee and delete) accepts an optional `expectedVersion` and
  rejects a stale one with `WorkItemVersionConflictException` (409). A
  `DbUpdateConcurrencyException` during save is translated to the same
  exception. A delete checks the version of the item named, not of its
  descendants.
- `expectedVersion` is optional so a caller that doesn't track versions
  still works, last-write-wins. `xmin` moves on *any* update, so a client
  that sends it for a status or parent change will have a card drag
  rejected when someone else edited the description; the client is
  expected to reload the item and tell the user (ticket 36).
- **Field updates use patch semantics.** `/details`, `/schedule` and
  `/tags` (and `update_work_item_details`, `reschedule_work_item`,
  `set_work_item_tags` over MCP) change only the fields sent: a field left
  out is unchanged and `null` clears it. The title can't be cleared. In
  code the difference is `Optional<T>` (`Weaver.Domain`): REST reads it
  with `OptionalJsonConverterFactory`, and MCP tools read it from the raw
  call arguments (`McpArguments.Supplied`), because the SDK binds a
  missing argument and `null` to the same parameter value.
- **Writes return `{ item, changedFields }`**: the item with its new
  version, and the fields whose value actually changed (`WorkItemField`
  names, matching the JSON). Setting a field to its current value isn't a
  change, doesn't move the version and isn't listed. The domain methods
  report the changed fields, so the services don't diff.
- The detail dialog sends `expectedVersion` for everything it edits,
  including the assignee and delete; on a conflict it reloads the item and
  re-seeds its form fields so the next save can't overwrite the other
  change with stale values. The board's own calls don't send a version
  yet, because board cards don't track one (ticket 36).

## Authentication

- **Tokens:** a JWT access token (15 minutes,
  `JwtOptions.AccessTokenLifetime`) and an opaque refresh token (30 days,
  `JwtOptions.RefreshTokenLifetime`). Only the refresh token's SHA-256
  hash is stored (`RefreshToken.TokenHash`), so a database read alone never
  yields a usable credential.
- **Signing key:** `Jwt:SigningKey` is required; the API refuses to start
  without one or with one under 32 bytes (HS256's minimum), which would
  otherwise only fail at the first login. The dev key and connection
  string live only in `appsettings.Development.json`, and
  `JwtSigningKeyPolicy` also rejects the public dev key outside
  Development, so a deployment that forgets `Jwt__SigningKey` fails to
  start instead of signing tokens anyone could forge.
- **Passwords** are hashed with `PasswordHasher<User>` (PBKDF2-HMAC-SHA256,
  framework-managed iterations) from the standalone
  `Microsoft.Extensions.Identity.Core` package. The full ASP.NET Core
  Identity system (roles, claims, its EF schema) would impose far more
  shape than a username/password table needs. Passwords are 12–200
  characters; usernames 3–50 of letters, digits, `.`, `_`, `-`, compared
  case-insensitively.
- **Login failures are uniform.** Unknown username, wrong password and a
  locked-out account all throw `InvalidCredentialsException` with the same
  message, and the first two paths also verify the password against a
  dummy hash so they take as long as a real check. Neither the response
  nor its timing tells a caller whether a username exists or is locked.
  Only an admin creating an account learns that a name is taken (409).
- **Lockout:** five consecutive failures lock the account for 15 minutes
  (`User.RecordFailedLogin`). An expired lockout starts a fresh count, so
  the first mistake afterwards doesn't re-lock immediately.
- **Auth endpoints are rate-limited per client IP** (`login`, `refresh`,
  `logout`): 30 requests a minute by default, configurable under
  `RateLimiting:Auth`, answered with 429, a `Retry-After` header and a
  problem-details message the login screen shows. The limit is generous
  enough for a team behind one NAT; the per-account lockout is what stops
  guessing against one user.
- **Every endpoint requires authentication by default**, through
  `AddAuthorization(options => options.FallbackPolicy = ...
  RequireAuthenticatedUser())`. A new controller is protected
  automatically. `AuthController` opts out per action with
  `[AllowAnonymous]` (login, refresh, logout); `/health` opts out
  with `.AllowAnonymous()`.
  - **Pitfall:** never put `[AllowAnonymous]` on a controller that has
    authenticated actions. It wins unconditionally anywhere in the chain;
    an action-level `[Authorize]` (like `Me`) does not override it. The
    `ASP0026` analyzer warning flags this.
- **`options.MapInboundClaims = false` on `AddJwtBearer` is required.**
  Otherwise the handler remaps `sub` to a legacy XML-namespace claim type,
  and `ClaimsPrincipalExtensions.GetUserId` (which reads
  `JwtRegisteredClaimNames.Sub`) never matches a real token. Unit tests
  that build a `ClaimsIdentity` by hand bypass the handler and won't catch
  this.
- **Refresh rotation:** every `RefreshAsync` revokes the token it was
  given and issues a new one; `ReplacedByTokenHash` records the chain.
- **Reuse detection revokes the chain, not the account.** A rotated token
  presented again means two parties hold it, so `RefreshAsync` walks
  `ReplacedByTokenHash` forward and revokes every token issued from it.
  The user's other sessions (separate logins) are untouched. Logged-out
  tokens have no replacement and are simply rejected.
- **30-second grace window.** A rotated token that comes back within 30 s
  is rejected with 401 but doesn't trigger chain revocation. One app
  instance shares a single in-flight refresh, but two browser tabs share
  the cached refresh token and can both refresh at startup; without the
  window, that would revoke the session in both.
- **`RefreshToken.Version` maps to `xmin`**, so two simultaneous refreshes
  with the same token can't both mint a new pair: the loser's
  `SaveChanges` fails and it gets a 401.
- **Expired refresh tokens are purged per user whenever new ones are
  issued**, instead of by a background job. Revoked but unexpired tokens
  are kept, because reuse detection needs them.
- **CORS** allows only `Cors:AllowedOrigins`, with no default, so a
  deployment that forgets it allows no cross-origin calls rather than all
  of them. The web app is served same-origin with the API (nginx proxies
  `/api`), so this only matters for another web origin calling the API
  (`CORS_ALLOWED_ORIGIN`, defaulting to `https://$WEAVER_DOMAIN`).
- **Comment edit/delete authorisation is in `CommentService`**, not the
  controller: it reads the caller from `ICallerContext` and throws
  `CommentAuthorMismatchException` (403) for anyone but the author. The
  author of a new comment is the caller too.
- **`ICallerContext` (Application) says who is calling**: account, API key
  and its scope (for an agent signed in with one), actor label and run id.
  `HttpCallerContext` (Api, scoped) reads the first three from the
  authenticated principal's `sub`, `weaver_api_key_id` and
  `weaver_api_key_scope` claims, so REST and MCP share one mapping.
  The actor label and run id come from the `X-Actor-Label` and
  `X-Agent-Run` headers on REST, or from an MCP tool's `actorLabel` and
  `runId` arguments, which the tool hands over with `UseAttribution` (an MCP
  client sets headers per connection, not per call). An argument left out
  falls back to the header. It's read lazily and throws `InvalidAccessTokenException` without a
  signed-in caller, so the background worker can still resolve the services.
  Every service that changes a work item takes it as a required argument,
  because its history names the caller. The lookup services and
  `UserAdministrationService` still also take `actorUserId` for their admin
  check, so the caller is named twice there until that check reads the
  context instead.

### Administrators and accounts

- **`User.IsAdmin`** gates the Admin page and every lookup or account
  change. Before admins existed everyone could do everything, so the
  `AdminUsersAndDeactivation` migration made every existing account an
  admin. Its `Down` drops the flag, so going down and up again makes
  everyone an admin.
- **There is no self-registration.** An admin creates every account
  ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md)); the API has no
  register endpoint and the login screen offers none.
- **The first admin comes from configuration.** On every start, after
  any startup migration, `FirstAdminBootstrapper` checks the database: if
  it has no accounts at all and `FirstAdmin:Username` and
  `FirstAdmin:Password` are set, it creates that account as an admin; if
  they aren't set it logs a warning. Once any account exists the settings
  are ignored, so they can be left in place or cleared. The password must
  follow the usual rules, or startup fails. Development sets
  `admin` / `development-only-admin-password` in
  `appsettings.Development.json`; the example `compose.yaml` passes
  `WEAVER_FIRST_ADMIN_USERNAME` / `WEAVER_FIRST_ADMIN_PASSWORD` from
  `.env`. A configuration value was chosen over a CLI command because the
  reference deployment (Komodo) is driven by environment variables, not
  interactive shells.
- **The admin check reads the database** (`IAdminAuthorizer`), not a token
  claim. Revoking someone's admin rights takes effect on their next
  request, not when their access token expires. It refuses every agent
  account (`AgentAdministrationException`, 403) before looking at
  `IsAdmin`; see [Agent accounts and API keys](#agent-accounts-and-api-keys). The client's `isAdmin`
  (from the auth response) only decides whether the gear is shown.
- **`/api/admin/users`** lists every account (including deactivated ones,
  with any lockout still in force) and creates, renames, resets passwords,
  unlocks, grants or revokes admin, deactivates or reactivates, and
  deletes. Username and password rules are `UserCredentials`, shared with
  the configured first admin.
- **Deactivate instead of delete.** Comments must keep their author
  (`Restrict`), so deactivation is the normal way to remove someone:
  `DeactivatedAtUtc` blocks login (the same uniform
  failure as a wrong password) and refresh, revokes their refresh tokens,
  unassigns their work items, drops them from `GET /api/users` (the
  pickers), and blocks assigning them again (400). Hard delete exists but
  is refused for a user who has written comments
  (`UserHasCommentsException`, 409), rather than losing their history.
- **An access token outlives deactivation by up to 15 minutes.** Only
  refresh tokens are revoked; checking the user on every request would add
  a database read to all of them. The same applies to a password reset.
- **Server settings are one row** (`ServerSettings`, seeded by its
  migration and held to a single row by a check constraint), with a typed,
  validated column per setting rather than key/value pairs. Today that's
  the time zone (default `UTC`). Only IANA ids are accepted: Windows ids
  resolve on a Windows host but not in the Linux container, and the stored
  id has to work in production. Anyone signed in can read the settings
  (`GET /api/server-settings`); only admins change them, one setting per
  `PUT` (`/api/server-settings/time-zone`), through `IServerSettingsService`.
- **Nobody can lock the team out of administration.** The last active
  admin can't be demoted, deactivated or deleted (`LastAdminException`),
  and an admin can't deactivate or delete their own account
  (`OwnAccountChangeException`); the page doesn't offer either.

### Agent accounts and API keys

- **Agents are rows in `Users`** with `Kind = Agent`, a free-text name
  (stored as the username, so it shares the unique index with humans) and
  an optional `Description`, made by `User.CreateAgent`. Keeping one table
  means `FirstAdminBootstrapper`'s "no accounts yet" check and every
  user lookup work unchanged. Agents have no password hash:
  `User.SetPasswordHash` refuses them, so a reset through `/api/admin/users`
  is a 409 (`NotAHumanAccountException`), and `AuthService.LoginAsync`
  refuses them with the usual uniform failure anyway, for rows given a
  password before that. Renaming one through `/api/admin/users` still
  applies the human username rules.
- **`/api/admin/agents`** (`IAgentAccountService`, admins only) lists and
  creates agents, deactivates or reactivates them (`PUT {id}/active`, which
  reuses the human rules: unassign, end sessions), and lists, issues
  (`POST {id}/keys`) and revokes (`DELETE {id}/keys/{keyId}`) their keys.
  Any of these on a human's id is a 409 (`NotAnAgentAccountException`), and
  `ApiKey.Issue` refuses a human itself.
- **An `ApiKey` stores only a SHA-256 of the key**, unique-indexed for the
  per-request lookup. A key is `wvr_` plus 256 random bits (`ApiKeySecret`),
  so a slow password hash would add cost without adding safety. The
  plaintext is in the create response only (`IssuedApiKeyDto`). Revoking
  sets `RevokedAtUtc` and keeps the row, so history can still name the key.
  `LastUsedAtUtc` is written at most once a minute per key
  (`ApiKey.RecordUse`), so a busy agent doesn't turn every read into a write.
- **Keys arrive as `Authorization: Bearer wvr_…`**, the header every MCP
  client can set. The default scheme is a policy scheme that forwards a
  `wvr_` bearer value to `ApiKeyAuthenticationHandler` and anything else to
  JWT bearer, so each credential is only checked by its own handler. The
  key handler (`IApiKeyAuthenticator`) checks the key, its revocation and
  the account's activity on every request, so a revoked key or a
  deactivated agent is refused at once (401), unlike an access token. Its
  principal carries `sub` like an access token, so `[Authorize]`,
  `GetUserId()` and `IAdminAuthorizer` treat both alike.
- **Deactivating an agent doesn't revoke its keys**; they stop working
  while it's inactive and work again if it's reactivated.
- **A read-only key reads everything and writes nothing.** The key's scope
  is read from its row while it's authenticated and carried as a
  `weaver_api_key_scope` claim, so checking a write needs no second lookup.
  The rule is one method, `Caller.EnsureCanWrite`
  (`ReadOnlyApiKeyException`, 403), applied once per transport, each
  covering every endpoint by default:
  - **REST:** `WriteAccessFilter`, a global MVC authorization filter, for
    every action that isn't GET, HEAD or OPTIONS. It runs before model
    binding, so a read-only key gets the same 403 whatever it sends.
  - **MCP:** `WriteAccessToolFilter`, a call-tool filter, for every tool not
    marked `ReadOnly = true`, answering with a tool error. Every MCP call is
    a POST to `/mcp`, so the HTTP method can't tell reads from writes there.
    A tool that doesn't say counts as a write; marking a write tool
    `ReadOnly` would open a hole, so an integration test allows the
    annotation only on `list_`/`get_`/`whoami` tools.
  - Why not in each service: a call per write method is easy to forget on
    the next one, and a `SaveChanges` hook would also catch the key
    lookup's own `LastUsedAtUtc` write and turn a forbidden write into a 400
    or 404 when validation ran first.
- **No agent changes admin data, whatever its scope.** Every admin
  operation already goes through `IAdminAuthorizer`, over REST and MCP
  alike, so that one check refuses agents. `User.GrantAdmin` refuses them
  too (409), so an agent can't be made an admin. An agent flagged as an
  admin before that rule doesn't count as "another admin" when the last
  human admin is demoted, deactivated or deleted. MCP has no admin write
  tools at all, and an integration test holds every MCP write tool to work
  items and comments. Creating and renaming Spaces is admin-only too. A
  new admin write (Spaces' statuses, Types,
  retention) must call `IAdminAuthorizer`; purging the Trash is human-only
  but not admin-only, so it needs its own agent check.

## MCP

- **Tools live in `Weaver.Api/Mcp/`**, not a separate project, because
  they return the same `Weaver.Api.Contracts` DTOs the controllers use.
  Each tool class mirrors one controller (`WorkItemTools` ↔
  `WorkItemsController`, etc.) and takes the same service interface. The
  SDK (`ModelContextProtocol.AspNetCore`) resolves a tool instance per call
  from the request's DI scope, so a `WeaverDbContext`-backed service
  behaves exactly as it does for a controller. Services read the caller
  from `ICallerContext`, so no tool passes a user id.
- **Every tool that changes a work item takes an optional `actorLabel`**
  (at most 100 characters) and passes it to `HttpCallerContext`, so its
  history entry shows which part of an agent made the change. It takes an
  optional `runId` the same way, so one run's changes can be reported
  together (ticket 50).
- **Coverage:** work items, Spaces, statuses, Types, priorities, users,
  comments, links, repeating items and server settings. Spaces, lookups,
  users and server settings (`get_server_settings`) are read-only over MCP, and agents
  will never be able to change admin data
  ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md)). A read tool must
  be marked `ReadOnly = true`, or a read-only key is refused it
  ([Agent accounts and API keys](#agent-accounts-and-api-keys)). Auth endpoints are
  not tools; they're how a client gets its token. `whoami` names the
  caller's account and kind, and the label and scope of the key it used.
  `list_all_work_items` pages through every item like `GET
  /work-items/all` (same `offset`/`limit`, default 500, at most 1000, from
  `WorkItemService`), so an agent can rebuild the whole tree from each
  summary's `parentId`; `list_work_item_children` with `parentId` omitted
  returns top-level items.
  `list_changes_since` pages with a cursor instead (see
  [Change history](#change-history)). `search_work_items` runs the
  [work-item query](#work-item-query) and returns summaries; it's how an
  agent filters, searches text or looks up a `#Number`.
  `list_unresolved_when_items` runs the same query with
  `UnresolvedWhenOnly` (ADR-0003): Weaver has no LLM, so an agent reads
  each phrase and resolves it with `reschedule_work_item` or a BlockedBy
  `create_work_item_link`, and the item drops off the list. Its
  description tells agents to find a referenced item with
  `search_work_items` before linking and to leave ambiguous phrases to a
  human. Summaries carry the `when` phrase for this.
  `list_views` and `run_view` list the saved Views and run one (see
  [Views](#views)).
  `list_dependency_conflicts` (optionally per Space, matching either side
  of the link) names each conflicting blocked item and its blocker with
  their numbers, titles, Spaces and dates; its description tells agents
  to raise a conflict with a human rather than reschedule either item.
- **Lists return summaries; `get_work_item` returns the full item.** Every
  list tool that returns work items (`list_work_item_children`,
  `list_all_work_items`, `search_work_items`, and the item in each
  `list_trash` and `list_changes_since` entry) returns a `WorkItemSummary`,
  loaded through the one `Summarize()` projection, so names and the child
  count cost no query per item (`SummaryQueryCountTests` counts them on
  Postgres). `get_work_item` takes an `id` or a `number` (an int, the `#N`
  without the `#`, matching `search_work_items`), exactly one of the two.
  `list_repeating_work_items` and `list_work_item_links` keep their own
  brief references to items (number, title), which never were full items.
- **`get_work_item_context` is the one-call read.** It takes the item like
  `get_work_item` (both resolve through `WorkItemReference`) and an
  `include` list of `ancestors`, `children`, `comments`, `links` (work-item
  links as `links`, URL links as `urlLinks`), `history` and `attachments`,
  and reads only what's included. A list asked for but empty comes back as
  `[]`; one not asked for is left out. Include values are strings checked
  by `WorkItemContextIncludes` rather than an enum argument, so a wrong one
  gets an error listing the valid ones instead of the SDK's generic binding
  failure. Ancestors (`IWorkItemService.GetAncestorSummariesAsync`) run
  root first and children in board order, both as `WorkItemSummary`;
  trashed items are left out of both, and out of links, by the usual Trash
  filter. History is `IWorkItemHistoryService.ListPageForWorkItemAsync`:
  newest first, paged in the query (it reads one entry past the page to
  know whether another follows), `historyLimit` 20 by default (at most
  200), and `nextHistoryOffset` says where the next page starts. Attachments are
  metadata only; content is `get_work_item_attachment`'s job.
- **Agents learn Weaver from the server itself.** `AddMcpServer` sends
  `Mcp/ServerInstructions.md` as the server instructions, which clients put
  into every conversation, so they stay under about 500 tokens: what
  Weaver's terms mean, patch semantics, `expectedVersion`, the actor label,
  and when to ask a human. The fuller `Mcp/Reference.md` (vocabulary from
  the glossary, every tool, step-by-step workflows) is the MCP resource
  `weaver://reference` (`ReferenceResource`), read on demand. Both are
  embedded Markdown read through `AgentGuidance`. `AgentGuidanceTests`
  holds the instructions to the token budget and checks that both name
  only real tools and that the reference lists every tool, so adding a tool
  means adding it to `Reference.md`. Resources aren't tool calls, so a
  read-only key reads the reference too.
- **MCP results leave nulls out**
  (`DefaultIgnoreCondition = WhenWritingNull` in
  `McpJsonSerializerOptions`); a missing property means null. REST keeps
  writing nulls. A property whose null is the information opts back in
  with `[JsonIgnore(Condition = Never)]`, as history values do. Write
  results list only the names of changed fields, so a cleared field just
  goes missing from the returned item.
- **Auth is the REST API's.** `app.MapMcp("/mcp")` sits in the same
  pipeline after `UseAuthentication()` / `UseAuthorization()`, so it
  inherits the fallback policy. A request with no bearer value, or a bad
  one, gets a plain 401 before reaching MCP handling. Agents send their API
  key ([Agent accounts and API keys](#agent-accounts-and-api-keys)); a
  human's access token works too.
- **Pitfall: enum serialisation options.** `WithToolsFromAssembly` takes
  its own `JsonSerializerOptions`; without one, enums go out as ints.
  `McpJsonSerializerOptions.Default` adds `JsonStringEnumConverter`, and it
  must be copy-constructed from `JsonSerializerOptions.Default`. A bare
  `new JsonSerializerOptions()` has no `TypeInfoResolver`, and the SDK's
  `MakeReadOnly()` call then throws at startup, deep inside `MapMcp`, so
  the whole API fails to start. No unit test exercises the real DI graph,
  so check any options object handed to a DI-resolved SDK feature by
  actually starting the app.
- **Domain exceptions become `McpException`.** The SDK strips messages from
  exceptions it doesn't recognise, so a not-found error reaches the client
  as a blank failure. `McpExceptionTranslation` rethrows known domain
  exceptions as `McpException(ex.Message, ex)`, whose message is
  propagated. These are the same messages REST sends in its 4xx bodies, so
  nothing new is exposed. This is per-transport presentation, not
  duplicated business logic.
- **Stateless Streamable HTTP** (`options.Stateless = true`) on the API's
  own port, not a separate stdio process. Each MCP call is an ordinary
  request with no server-side session, matching the stateless REST API. In
  production it's reached through the frontend's nginx, which proxies `/mcp`
  unbuffered with a one-hour read timeout because responses can be
  server-sent event streams.

## Frontend

### Structure and DI

- **Features don't import each other.** `app.dart` is the composition
  root: it builds `AuthGate` with the signed-in screen, and gives
  `BoardView` a `WorkItemDetailOpener` (`showWorkItemDetailDialog`) and,
  for admins only, an `AdminPageOpener` (`showAdminPage`). `AuthGate`
  passes the signed-in `User` to its builder and rebuilds on session
  changes, so a refresh that changes the admin flag shows or hides the
  gear. The
  detail view model learns who is signed in through `CurrentUser`, which
  `AuthSessionStore` implements (bound in `NetworkModule`).
- **Statuses and users are fetched through shared, cached repositories**
  (`StatusRepository`, `UserDirectoryRepository`) that the feature
  repositories delegate to. Statuses are cached per Space until `invalidate()`,
  which the Admin page's repositories call after every successful write.
  The user list works the same way: only admins change accounts, and the
  Admin page invalidates it after each change. A failed fetch is never
  cached. Another person's admin changes show up after a reload.

- **`ViewModel`** (`lib/core/presentation/view_model.dart`) is a thin
  `ChangeNotifier` that adds `notifyIfActive()`: async work can finish
  after the view is disposed, and `notifyListeners()` after `dispose()`
  throws. Views resolve their view model with `getIt<T>()` (registered
  `@injectable`, a new instance per resolution), rebuild with
  `ListenableBuilder`, and dispose it themselves. No state-management
  package is used; the SDK's primitives are enough.
- **DI uses `get_it` + `injectable`, kept on the 2.x line**
  (`injectable` 2.7.1+4, `injectable_generator` 2.9.1).
  `injectable_generator` 3.x moved to `lean_builder`, which needs
  `source_span ^1.10.2`, while the Flutter SDK's `flutter_test` pins
  `source_span 1.10.1`, so 3.x can't be resolved at all. Revisit when
  either side moves. Regenerate `lib/core/di/injection.config.dart` with
  `dart run build_runner build` after changing annotations.
- **Lint rules are enforced**, including `always_use_package_imports` and
  `specify_nonobvious_property_types` (so `final x = getIt<Foo>();` needs
  a type). CI fails on any `flutter analyze` issue.

### Networking

- **One HTTP/JSON path.** Every `Api…Repository` wraps its injected
  `http.Client` in `JsonApiClient` (`core/network/json_api_client.dart`),
  which builds URIs, checks status codes, reads the problem-detail message
  and decodes; models parse themselves with `fromJson` factories. Every
  failure is an `ApiException`: a non-2xx response keeps its
  `statusCode`, an unreachable server is a `NetworkException`, and a body
  that doesn't match the contract is an `UnexpectedResponseException`, so
  a backend contract change isn't reported as "check your connection".
- **View models catch `ApiException`, not everything.** A `TypeError` or
  `StateError` from app code propagates instead of being shown as a
  failed request. A record `.wait` wraps failures in `ParallelWaitError`;
  `rethrowNonApiErrors` rethrows anything in it that isn't an API
  failure. A token refresh only clears the session on a 401, so a network
  blip or a parsing bug doesn't log the user out.
- **Repositories behind interfaces** (`BoardRepository`,
  `WorkItemDetailRepository`, `AuthRepository`), each with an `Api…`
  implementation. View models depend only on the interface; tests use
  small stubs. The detail repository is separate from the board's because
  its concerns (full record, Types, users, per-field saves) differ from
  swimlane assembly and drag-and-drop.
- **Base URL:** `ServerSettings` decides it and always ends it in `/api`.
  On web it defaults to the relative `/api`, so the same built image
  works at any domain (nginx proxies it), and it can't be changed. Native
  builds ask for the server on the sign-in screen: a missing scheme means
  https, a pasted `/api` is dropped, and the address is remembered (in
  secure storage, next to the refresh token) once sign-in succeeds. An
  optional `--dart-define=API_BASE_URL=http://host:port` is the starting
  value on both, and is how the web dev server reaches the backend.
  Getting this wrong makes every request 404 with nothing but "Failed to
  load resource" in the console.
- **Repositories read the server per request.** They take an
  `ApiEndpoint` (bound to `ServerSettings`) rather than a URL string,
  because on native the server is chosen at sign-in, after the
  repositories exist. The status and user caches note which server they
  were filled from and refetch when it changes. Tests pass a fixed
  endpoint and `http`'s `MockClient`.
- **Anything thrown before a response is a `NetworkException`.** On
  native, `IOClient` wraps socket errors in `ClientException` but lets TLS
  handshake failures through as `HandshakeException`, which
  `JsonApiClient` can't name because it's also compiled for web.
- **Native apps trust only the system CA store.** Dart's `HttpClient`
  ignores user-installed CAs on Android, so the self-signed Caddy CA
  from the TLS setup works for browsers but not the app. Point the app at
  a server with a publicly trusted certificate, such as a Tailscale
  `*.ts.net` address served with `tailscale serve`.
- **`AuthHttpClient` is the default `http.Client` binding**
  (`NetworkModule`). It attaches the access token and refreshes before
  each call, so every repository is authenticated without auth code of its
  own. `ApiAuthRepository` gets the separate `@Named('rawHttpClient')`,
  because refresh must never go through the wrapper that calls it.
- **One shared refresh.** `AuthSessionStore.ensureValidSession` keeps the
  in-flight refresh `Future` and hands it to every concurrent caller. The
  backend rotates refresh tokens, so parallel refreshes with the same token
  would all but one fail, and each failure used to clear the session the
  winner had just stored. A failed refresh, or a 401 in `AuthHttpClient`,
  only clears the session if it's still the one the request started with.
  Access tokens count as expired 30 s early (`AuthSession.expiryLeeway`)
  so they can't expire in flight.
- **Refresh-token persistence is best-effort.** `flutter_secure_storage`
  calls are wrapped in try/catch in `AuthSessionStore`, and
  `setSession` / `clear` update state and notify listeners *before*
  persisting. A storage failure once silently broke logout because the
  write was awaited first.
- **The detail dialog has two save modes, on purpose.** Title,
  description, Type and priority form one "details" form saved with
  **Save details** (enabled only when it differs from the saved item);
  assignee, tags, schedule, comments and links save as soon as they
  change. Closing with unsaved details (back, tapping outside, or "View
  sub-items") asks before discarding them (`PopScope`). A blank title and
  an inverted schedule are rejected before any request, and a 400 shows
  the server's validation message.
- **The detail dialog has Details and History tabs.** History
  (`WorkItemHistoryViewModel`, `HistoryTab`) loads when the tab is shown
  and again on every visit, so changes just saved on Details appear; it
  isn't fetched at all if nobody opens it. Each row shows who (with the
  shared `UserStateBadge` marking agents, plus the actor label and key
  they sent), when, and the change. `WorkItemHistoryEntry.change` turns
  the raw JSON values into text, preferring the names the server sends
  for id values.
- **User-written Markdown goes through `MarkdownText`**
  (`shared/markdown/`, on `flutter_markdown_plus`, the maintained fork of
  the discontinued `flutter_markdown`). Raw HTML stays text, images show
  their alt text rather than being fetched from wherever the author
  pointed them, and only `http`, `https` and `mailto` links open, outside
  the app (`url_launcher` opens them with `noopener` on web).
- **Ticking a task box edits one source line.** `toggleTask`
  (`shared/markdown/task_list.dart`) is a pure function that finds task
  lines the way the renderer does (skipping fenced and indented code) and
  flips the nth one. The renderer builds a list item's box *after* its
  nested items, so boxes are numbered by their order in the element tree
  (`TaskToggleScope`), not by builder calls. Where the line scan and the
  renderer's box count disagree (e.g. a task line inside an HTML block),
  ticking is turned off rather than risk changing the wrong line. A tick
  sends only the description with `expectedVersion`, built from the
  saved description, so it never carries the form's unsaved edits; the
  boxes are read-only while the description has unsaved edits or a save
  is in flight.
- **Comment and link changes in the detail dialog apply the server's
  response locally** instead of reloading the list. A reload that failed
  after a successful POST used to show as a failure, and retrying created
  a duplicate.
- **Day arithmetic goes through `core/dates/calendar_days.dart`**
  (`dateOnly`, `addDays`, `daysBetween`). `add(Duration(days: n))` adds
  24-hour blocks, so across a DST change a local date lands at 23:00 or
  01:00, and `difference().inDays` truncates 23 hours to zero. The
  Roadmap's window, headers, today marker and bars (`roadmapBarSpan`) and
  the time filter all compare calendar days.
- **`core/network/api_dates.dart` owns the wire format for dates.**
  Schedule dates are `yyyy-MM-dd` and parse to local midnight
  (`parseCalendarDate` / `formatCalendarDate`); every other timestamp is
  parsed and converted with `.toLocal()` (`parseApiTimestamp`).

### Board view model

- **The view model orchestrates; the logic is in pure units** under
  `board/state/`: `BoardFilters` (an immutable value holding the time
  range, search, hidden statuses, tags and sort, with every visibility
  predicate), `sortComparator` (one comparator for cards and Hierarchy
  items), `buildHierarchyTree`, `SwimlaneEdits` (id-based lane edits for
  optimistic updates) and `UserDirectory` (id lookups). Each is tested on
  its own.
- **Derived values are memoized.** `hierarchyRoots`, `visibleStatuses`
  and `availableTags` are cached until the next change that can affect
  them, and status and user lookups are map lookups, so a rebuild doesn't
  redo the tree or scan the user list per card. Search input is debounced
  by 200 ms in `BoardView`.
- **An item edit rebuilds only what shows it.** The view model has three
  signals:
  - **Itself**, for what reshapes the board: loading, scope, statuses,
    filters, and the lane list (a lane added, removed, reordered or
    renamed, since the drill-down targets show lane titles). This rebuilds
    the header and every lane.
  - **`swimlaneListenables`**, one `ValueListenable<Swimlane>` per lane.
    After every store change each lane is redrawn and compared by value
    (`Swimlane` and `WorkItemCard` have `==`), so only a lane whose cards
    actually changed fires. `SwimlaneView` gives each lane's label and row
    their own `ValueListenableBuilder`; both measure the lane's height,
    so neither rebuilds for another lane.
  - **`itemChanges`**, fired after any store change, for what draws from
    every item: Hierarchy, Roadmap and the Filters dialog's tag list.
    Hierarchy and Roadmap are only built while their tab shows.

  `swimlane_rebuild_test.dart` counts `StatusColumn` rebuilds per lane to
  hold this.

- **`BoardViewModel.moveCard(card, newStatusId, beforeCardId:)` takes no
  lane parameter**; it uses the card's own `parentId`, so a status move can't
  cross lanes. The UI adds a second layer: the `StatusColumn` drag target
  only accepts cards whose `parentId` matches its lane. Reparenting uses a
  separate drag target on `SwimlaneLabel` that only accepts cards from
  *other* lanes. At model level, `WorkItemCard.copyWith` never touches
  `parentId`; `movedToParent` is the separate reparent path.
- **Scope changes go through `_changeScope`.** `load`, and through
  `_navigate` `openView`, `setSpaces`, `focusSpace`, `drillIntoScope`,
  `navigateToBreadcrumb` and `refreshCurrentScope`, all use it: it sets the
  loading flag, catches failures into `loadError`, and records a `_retry`
  closure **only when the action fails**.
  - `retry()` re-attempts the failed navigation and is a no-op otherwise.
    Use `refreshCurrentScope()` to reload what's shown. `retry()` used to
    double as refresh and replayed whatever ran last, pushing a breadcrumb
    again or re-creating an item.
  - `createWorkItem` deliberately doesn't use `_changeScope`: a failed
    create reports through `moveError` and keeps the board, and a create
    must never become the retry target.
  - `load()` loads the Spaces and opens the user's default View
    (`GET /api/views/default`; Overview until they pick another). It
    replaced remembering the last-used Space on the device: a default
    View is chosen on purpose and follows the user to every device.
  - **The breadcrumb runs View, Space, drill-down path**
    (`ScopeCrumb.spaceId`). A View of several Spaces starts at its own
    root crumb, titled with its name, showing every Space; a View of one
    Space starts at that Space. Drilling into a lane from the View's root
    pushes the lane's Space and then the lane; tapping a Space's heading
    (`focusSpace`) pushes just the Space. Later navigation reuses ids
    already known client-side, and a crumb's title is the title captured
    at drill-in.
  - `_navigate` shows a trail's last crumb. When that changes which
    Spaces are shown, or new filters come with it (opening a View,
    `setSpaces`), it drops what the server matched and the
    Hierarchy/Roadmap items (discarding a load in flight) before asking
    again, so another Space's items never show. Hierarchy and Roadmap load
    the shown Spaces' items, so they follow the breadcrumb's Space.
    With no Space at all there's no scope: the board says one has to be
    created, and `refreshAfterAdminChanges` opens the first one.
- **Generations guard against overlapping loads.** Every scope load bumps
  `_scopeGeneration` and every hierarchy load bumps
  `_hierarchyGeneration`. A load's result, and the clearing of its
  spinner, is only applied if no newer load started meanwhile, so a slow
  older response can't overwrite a newer scope.
- **Items live once, in `WorkItemStore`** (`lib/shared/data/`, a lazy
  singleton). The board keeps only its layout: lanes, card order within
  them, and which ids the Hierarchy/Roadmap show. It draws every card, lane
  label and Hierarchy row from the store and redraws whenever the store
  notifies, so an edit made anywhere shows on every layout (narrowly: see
  "An item edit rebuilds only what shows it" above). A new
  work-item field goes on the shared `WorkItem` model only.
- **Every write goes through `WorkItemStore.write`**, which takes the
  `expectedVersion` from the stored copy and keeps the item the server
  returns.
  - Optimistic edits sit on top of the server copy rather than replacing
    it, so a failed one is dropped without undoing any other edit.
  - Writes to one item go one at a time, each with the version the
    previous answer returned: `xmin` moves on every update, so a second
    write sent early would be rejected as stale. Other items aren't held
    up.
  - A 409 reloads just that item (`WorkItemConflict.latest`), and the
    writes already queued behind it fail without being sent, since they
    were made against the stale copy. The board then puts the card where
    the reloaded copy says (another lane, or off the board) and says
    someone else changed it first.
  - Loads pass `asOf: store.revision` read before they started, so a slow
    load can't put back an older copy of an item saved meanwhile.
  - The store knows nothing about dependencies. `BlockedItemRefresher`
    listens to its saves (`addSaveListener`) and reloads the held items a
    saved item blocks when its dates or Done state change, since that can
    flip their conflict badge. The detail dialog uses the refresher the
    same way after a delete and after adding or removing a blocks link.
    Restore can't do this: a trashed item isn't in anyone's `blockedBy`,
    so the board's scope reload after a restore covers what's visible.
- **Card position is the board's own optimistic state** (`_save`'s
  `applyLayout`/`revertLayout`). The revert undoes only that card's move
  by id, and is skipped if the scope was reloaded since. Restoring a
  whole-list snapshot, as before, could undo a different move that had
  succeeded in the meantime.
- **The detail dialog reads and saves its item through the store**, so
  its saves are queued and versioned like the board's, and a delete
  removes the item from the store, which every layout then drops.
  `showWorkItemDetailDialog` only returns `true` for changes the store
  can't carry: repeat settings, which can create items, saved here or in a
  nested dialog. The board then refreshes the current scope and, if
  loaded, the hierarchy. After "View sub-items" only the hierarchy is
  refreshed: the drill-in already loads the new scope, and refreshing the
  old one would be the newer load and cancel it. A parent dialog reloads
  its Sub-Items list whenever a nested dialog closes.

### Admin page

- **`features/admin/`**, opened from the gear next to sign-out. One tab
  each for Statuses, Priorities, Types and Users, each with its own view
  model; statuses and the name-only lookups share `OrderedListViewModel`
  (load and drag-to-reorder), and `LookupAdminViewModel` serves both
  Priorities and Types through an injectable factory parameter
  (`LookupKind`). Types also have a colour (`LookupKind.hasColor`), edited
  in `showTypeDialog` with the `HexColorPicker` statuses use.
- **Statuses, Priorities and Types are managed one Space at a time.** A
  `SpacePicker` above the list chooses the Space (`showSpace`), and the
  Admin view passes every change of the Space list to `followSpaces`, which
  keeps the chosen Space or picks the first, so a newly created first Space
  shows up. With no Space there's nothing to add to. A fifth
  tab, Server, shows the server-wide settings
  (`ServerSettingsAdminViewModel`); the time zone is typed as an IANA id
  and checked by the server. Its model is `AdminServerSettings` because
  `ServerSettings` in core already means "which server the app talks to".
- **Agents have their own tab** (`AgentAdminViewModel`), and the Users tab
  lists people only: its rename, password and admin actions don't fit
  agents. An agent's keys open on a page of their own
  (`AgentKeysPage`, `AgentKeysViewModel` with the agent id as a factory
  parameter). The plaintext key goes straight from the issue call to the
  dialog that reveals it, never into view-model state, and that dialog
  can't be dismissed by tapping outside. Revoking reloads the list, since
  the server keeps revoked keys and answers with no body. Assignee pickers
  show agents with `UserNameLabel`'s badge.
- **Types are cached app-wide like statuses**, one list per Space
  (`TypeRepository` and `StatusRepository` both use `SpaceLookupCache`):
  the board needs a Space's Types on every scope load to colour cards, and
  the detail dialog reads the same list. Type writes on the Admin page drop
  the cache.
- **Reordering is optimistic**: the new order shows immediately and the
  old one comes back if the save fails. Drag handles start a drag
  straight away (`ReorderableDragStartListener`), since the default
  long-press is undiscoverable on desktop and clashes with the row's
  buttons.
- **Every change reports through its dialog or a snackbar**, using the
  server's message for 400/403/404/409 (e.g. "used by 3 work items",
  "last administrator") and a generic one otherwise
  (`adminErrorMessage`). Dialogs stay open on an error.
- **Closing the page reloads the board** (`refreshAfterAdminChanges`:
  users, Spaces, current scope, and the hierarchy if loaded), since
  Spaces, statuses and users can have been added, renamed, recoloured or
  removed.
- **Spaces are the first tab** (`SpacesTab`): add and rename, no delete
  or reorder. They're listed by name.

### Repeating items in the UI

- **The recurrence model, repository and `RecurrenceEditor` live in
  `shared/recurrence/`**, because both the detail dialog and the board's
  Repeating tab use them and features don't import each other. `DateField`
  and `FieldLabel` moved to `shared/widgets/` for the same reason.
- **Each has its own view model**: `RepeatSettingsViewModel` for the detail
  dialog's Repeat section and `RepeatingItemsViewModel` for the tab,
  rather than growing `WorkItemDetailViewModel` or `BoardViewModel`.
- **Repeat settings are saved explicitly** (Save repeat), like the details
  form and unlike the other detail fields: saving each change would send
  invalid in-between states such as "weekly on no days". A
  `RecurrenceDraft` holds the edit, keeps its weekdays while a month-based
  frequency is selected (so switching back doesn't lose them), and mirrors
  the server's validation.
- `GET /work-items/{id}/recurrence` returns 204 for "doesn't repeat";
  `JsonApiClient` decodes the empty body as null.
- The Repeating tab reloads on every visit, and a save or stop refreshes
  the board, since saving can create items straight away.

### Board, filters and views

- **Views open in the header's View menu** (`ViewMenu`,
  `SavedViewsViewModel`, ticket 23). Opening one replaces the header's
  query inputs with the View's (`BoardFilters.withView`): its Spaces,
  search, tags, tag match, show-done and Overview's rule; the time range,
  hidden statuses and sort stay, since a View doesn't store them. Its
  layout picks the tab. "Save as new View" stores the header's inputs and
  the current tab (`BoardFilters.toDefinition`; a tab that isn't a layout
  saves as Board), and "Save changes" overwrites the open View the same
  way. Rename keeps the rest of the stored definition rather than the
  header's. Overview offers neither. The Filters dialog gained the Spaces
  shown (`SpaceFilterBar`, nothing chosen means every Space) and
  Overview's rule (`ScheduledFilterBar`), so any header state can be
  saved. The Dart class is `SavedView`, as Flutter already has a `View`.
- **A board of several Spaces is one section per Space**
  (`BoardViewModel.boardSections`): a heading, then a `SwimlaneView` with
  that Space's own status columns and lanes, since each Space has its own
  statuses. Lanes stay one flat list (`_laneSpaceIds` maps each lane to
  its Space), so lane edits are unchanged; moving a card to another
  Space's lane is refused before anything is sent, as the server would
  refuse it. Creating needs one Space shown. The Hierarchy and Roadmap
  group their roots by Space (`hierarchySections`, `roadmapSections`)
  under a `SpaceHeaderRow`; the Roadmap's arrows skip those rows.
- **A board loads in one request per Space.** `GET /api/work-items/swimlanes`
  (`IWorkItemService.GetSwimlanesAsync`) returns one Space's scope's
  lanes, each with its cards, using two queries whatever the lane count; statuses come
  from the cached `StatusRepository`. It replaced one request per lane.
  Rank is only unique within a (Space, parent, status) cell, so the read queries
  order by `Rank, Number` to stay deterministic across columns.
- **Search, tag filter, done-hiding and the time-frame filter are asked
  of the server's query** ([ADR-0006](adr/0006-server-side-query-and-views.md)),
  which applies the date-window rule of
  [ADR-0003](adr/0003-when-and-backlog.md). Hidden status columns and
  sort only change the layout, so they stay in the client. Only a saved
  View persists the query inputs, and never the time range.
- **The time-frame filter is interval overlap, not containment.** Both
  bounds are inclusive whole days, each can be cleared on its own, and an
  inverted range can't be picked (and is swapped if set programmatically).
  It is not a sprint system: there is no fixed-length cycle, and the frame
  isn't saved.
- **The Roadmap asks the server for its own window**
  (`BoardViewModel.showRoadmapWindow`): the days its timeline shows, cut
  to the header's range when one is set (`BoardFilters.toRoadmapQuery`;
  intervals that pairwise overlap share a day, so the cut loses nothing).
  The Roadmap is time-framed, so Backlog items never show there, even with
  no header filter. Rows are the matches under their ancestors, so an
  undated parent comes back as the parent of a match and is drawn as a
  group row (bold title, no bar); a dated parent outside the window still
  shows as the context of a match, with no bar in view. Moving the window
  keeps the previous rows until the answer arrives, and closing the tab
  (`closeRoadmap`) stops the re-asking that every edit otherwise causes.
  `ServerMatches` holds each answer for the header and the Roadmap, and
  drops a slower answer to an older question. If the first answer for the
  Roadmap fails it has no rows to keep, so it shows an error with retry
  (`roadmapLoadError`) rather than a spinner.
- **The Backlog tab** (`BacklogViewModel`) asks for `backlogOnly` in the
  shown Spaces and groups the answer by parent: top-level items first
  (one group per Space while several show),
  then each parent by title. Parents it doesn't hold are loaded one by
  one, as they can be dated or Done themselves and so aren't in the
  answer. After any save, made there or anywhere else, it asks again
  while its tab shows, so an item given dates leaves the list because the
  server says so, not by a client rule. It listens for saves rather than every store change,
  because its own load writes to the store. "Set dates" is a date-range
  picker; the detail dialog's quick picks work too. It depends on
  `BacklogRepository`, the three calls it needs, which `BoardRepository`
  also provides.
- **`CardSortOption.manual` has a `null` comparator** and `StatusColumn`
  skips sorting entirely in that case. Dart's `List.sort` isn't stable, so
  an always-0 comparator could reshuffle the rank order. Sorting never
  touches the backend `Rank`.
- **Board cards have an exact height**, so the lane heights the board
  computes from card counts always match what renders (the label column
  and the columns area are separate widgets and must line up). The height
  is at least what a card's content needs, measured with the current
  text scale; titles show at most two lines, with the full title in a
  tooltip and the detail dialog.
- **Tapping a card opens its detail dialog**; drilling in is the dialog's
  "View sub-items" action, or the current breadcrumb's menu, which lists
  the lanes on screen (`drillDownTargets`) and calls `drillIntoScope`. The
  menu goes one level at a time rather than offering the whole tree, so
  the breadcrumb trail is always the real path down. Any item can be
  drilled into, even without
  children: there's no `hasChildren` flag, and computing one only to grey
  out leaves would cost an extra fetch or a denormalised count.
- **Drag and drop uses Flutter's `Draggable` / `DragTarget`**, no package.
- **Cards are placed where they're dropped.** Each card in a cell carries
  two drop zones (`CardDropZones`), its left and right halves: cards sit
  two to a row, so those are before and after in reading order. A drop
  passes the card it landed before, or none for the end of the cell (also
  what a drop on the cell's free space means), and
  `SwimlaneEdits.rankAnchorForDrop` turns that into the `afterId` for
  `PUT /work-items/{id}/status`. A reorder within a cell is the same call
  with the card's own status, so the existing rank and tie-respacing logic
  covers it and there's no separate reorder endpoint.
  - The zones cover each card's half of the gaps around it (the cell's
    `Wrap` has no spacing; every card slot pads itself), so a drop in a
    gap lands where the indicator there shows instead of falling through
    to the cell. Over the cell's free space the indicator shows after the
    last card, since that drop means the end.
  - The anchor is taken from the whole cell, hidden cards included, so
    the card lands directly before the card it was dropped on whatever
    the filters hide.
  - A drop that changes nothing (onto the card itself or beside where it
    already is) shows no indicator and sends no request, but is still
    accepted, so it can't fall through to the cell and send the card to
    the end.
  - While a sort is applied the cell isn't shown in rank order, so cards
    carry no drop zones and the cell only takes cards from other columns,
    appending them. Sorting still never touches `Rank`.
  - A failed move puts the card back after whichever of its old
    predecessors is now latest in the cell, else before whichever old
    successor is now earliest, else at the end (`withCardRestored`). No
    other card moves, so a move that succeeded meanwhile stays.
- **Roadmap is a hand-rolled Gantt widget** built from
  `BoardViewModel.roadmapRoots`, the same tree and sort as Hierarchy but
  limited to what the server matched for the visible window (see
  [Board, filters and views](#board-filters-and-views)). Its timeframe
  (zoom) and window are view state, reset when the tab is reopened.
- **Hierarchy columns:** the expand caret lives inside the Title column and
  indents with the title, as in a file tree, while Number and the columns
  after Title keep a fixed x position, so every resizable column stays
  aligned with its header at every depth. The indent is capped at the Title
  column's width (`hierarchyTitleIndent`), so a deeply nested item
  squeezes its own title instead of spilling into Status. Roadmap rows
  indent caret and title the same way.
- **Assignee avatars** are tappable everywhere they're shown.
  `AssigneeAvatar(showPlaceholderWhenUnassigned: true)` shows a silhouette
  on swimlane labels, Hierarchy rows and the assign dialog so every row has
  something to tap; board cards omit it to stay compact.
- **Tags can be added from any row that shows them.** Cards, Hierarchy
  rows and Roadmap rows end their tag row with an `AddTagButton` (room for
  it is reserved before tags are fitted) that opens `showAddTagsDialog`:
  type tags or pick from the tags already on the board, then Add.
  `BoardViewModel.addTags` merges them with `withTagsAdded`
  (`state/tag_additions.dart`, the server's trim and case-insensitive
  de-duplication) and saves through the optimistic `setTags`. It only adds;
  removing a tag stays in the detail dialog. The button is a 22 px circle,
  not a tag-sized "+" badge: the badge was too small to hit reliably in
  the browser, even though widget tests tapping its centre passed.
- **`debugShowCheckedModeBanner: false`**: the debug banner sat over the
  top-right app-bar actions and swallowed clicks on sign-out.

## Deployment and infrastructure

The published images are the product; `compose.yaml` is a public example
of running them. The reference deployment runs the images under its own
compose file on a homelab, managed by Komodo, with Tailscale names and TLS
from tsdproxy ([ADR-0008](adr/0008-images-first-deployment.md)).

### Compose file split

- **`compose.yaml` is the example deployment.** It only references
  pre-built images (`BACKEND_IMAGE` / `FRONTEND_IMAGE` at
  `WEAVER_IMAGE_TAG`), with no `build:` and no source mounts. A server
  using it runs `docker compose pull && docker compose up -d`.
- **`compose.override.yaml` is the dev overlay.** Compose loads it
  automatically on `docker compose up`; it swaps in `build:`, bind mounts,
  dev commands and published ports, and disables `caddy` and `backup` with
  a profile nobody activates. It must never be copied to the server.
- **The merge is per key, not a replacement.** `environment:` merges per
  variable and `ports:` is a union, so the override inherits every base
  value it doesn't set. Details and consequences are in
  [development.md](development.md#compose-merge-behaviour).
- **Required variables fail loudly.** `POSTGRES_PASSWORD`,
  `JWT_SIGNING_KEY`, `BACKEND_IMAGE` and `FRONTEND_IMAGE` use
  `${VAR:?message}`, and `.env.example` ships them empty, so a missing or
  unedited value stops startup instead of running with a known weak
  secret or an invalid image reference. `WEAVER_DOMAIN` is the exception:
  Caddy checks it at startup, because Compose interpolates the whole file
  before applying profiles and dev (which never runs Caddy) shouldn't need
  it.

### TLS and request path

- **In the example compose, Caddy with its internal CA terminates TLS.**
  It targets a LAN server with no public domain, where Let's Encrypt isn't
  an option. `caddy` runs
  `caddy reverse-proxy --from https://$WEAVER_DOMAIN --to frontend:8080
  --internal-certs`, owns host ports 80/443 and redirects HTTP to HTTPS.
  It's configured on the command line so the server still needs only
  `compose.yaml` and `.env`. The CA keys live in the `caddy-data` volume;
  each device trusts the root once (see the README).
- **The backend publishes no port in the example compose.** Everything,
  including `/mcp`, goes Caddy → nginx → backend, so nothing reaches the
  API without TLS. nginx passes Caddy's `X-Forwarded-Proto` through
  (falling back to its own scheme), and the API trusts forwarded headers
  from up to two proxies (`UseForwardedHeaders`, `ForwardLimit = 2`, known networks and
  proxies cleared because Compose IPs aren't fixed). There is no
  `UseHttpsRedirection`: TLS ends at Caddy, and in-container redirection
  only logged a warning.
- **The app calls the API same-origin.** nginx proxies `/api/` and `/mcp`
  to `backend:8080`, so no API URL is baked into the frontend image; the
  domain is only known at deploy time.
- **Both frontend images listen on 8080** so prod and dev share one mental
  model; only the host side differs (Caddy → 8080 in the example compose,
  `8082:8080` in dev).

### Frontend image (nginx)

- **Unprivileged:** runs as uid 101 on `nginxinc/nginx-unprivileged`.
- **`flutter build web --no-web-resources-cdn`**, so CanvasKit loads from
  the server instead of `www.gstatic.com`. A LAN server shouldn't depend
  on Google's CDN, and it lets the CSP keep scripts to `'self'`.
- **CSP:** `script-src 'self' 'wasm-unsafe-eval'` (CanvasKit compiles
  WebAssembly); `style-src` allows `'unsafe-inline'` because Flutter
  injects inline styles; `fonts.gstatic.com` is allowed for fonts and
  `connect-src` because the engine still fetches Roboto/Noto from there.
- **Security headers are set once at `server` level with `always`.** An
  `add_header` inside any `location` silently drops every inherited one.
- **Every app file is `Cache-Control: no-cache`.** Flutter's web output
  isn't content-hashed (`main.dart.js`, `canvaskit/`, `assets/` keep their
  names across releases), so long-caching any of it could mix versions
  after a deploy. Revalidation is a cheap 304 via ETag. `/api` and `/mcp`
  responses keep their own headers.
- **`/api/` accepts request bodies up to 30 MiB**, up from nginx's 1 MB
  default, to fit the 25 MiB default attachment limit plus multipart
  overhead. Raising `Attachments__MaxFileSizeMegabytes` past about 29
  needs this raised too. Other paths keep the 1 MB default, so nginx
  doesn't buffer large bodies to disk where nothing takes them.
- Unknown paths fall back to `index.html` for client-side routing.

### Backend image

- Multi-stage: publish on the .NET SDK, run on the Alpine ASP.NET runtime
  as the non-root `$APP_UID`.
- **Invariant globalisation** (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`)
  keeps the image small; it's a JSON API with no locale-aware formatting
  (that's the client's job). Revisit if the backend ever formats for
  locales itself, e.g. exports.
- **Its only state is the attachments path** (`Attachments__Path`,
  default `/data/attachments`). The image creates that directory owned by
  `$APP_UID`, because an empty named volume copies the ownership of the
  directory it's mounted over; otherwise the volume would be root-owned
  and the non-root API couldn't write to it. A host bind mount gets no
  such copy, so its directory must be owned by that uid (1654).

### Startup, health and backups

- **Startup is ordered by health.** The API exposes an anonymous `/health`
  with a database check, not proxied by nginx. Compose runs db → backend
  (which migrates before serving) → frontend → caddy (each on
  `service_healthy`), so Caddy never serves a frontend whose API isn't
  ready. There is no separate `migrate` service: it was the hardest part
  of the example to copy into another compose file. The dev override
  relaxes the frontend's wait to `service_started`, because the first
  `dotnet watch` build can outlast the healthcheck.
- **Backups belong to whoever runs the images**; the reference homelab
  does its own. The example compose has an inline `backup` service on
  `postgres:16-alpine`, which already has `pg_dump`. A third-party backup
  image would need the database password, and inline configuration keeps
  the server at `compose.yaml` + `.env`. Schedule, retention and restore
  are in the README. It only dumps Postgres: attachment files aren't in
  the database, and the deployment must back up the attachments path too
  (ADR-0008).
- **Attachments default to a named volume, not a `./attachments` bind
  like `BACKUP_DIR`.** Docker creates a missing bind directory as root,
  which the non-root backend can't write to, while a named volume takes
  the image's ownership. `ATTACHMENTS_DIR` accepts a host path for
  deployments that want the files on other storage.
- **A stale backup makes the `backup` service unhealthy.** A failed dump
  only logs and the loop carries on, so the container would otherwise look
  fine indefinitely. Its healthcheck fails once the newest daily dump is
  older than `BACKUP_MAX_AGE_HOURS` (default 36: a nightly schedule plus
  slack), which surfaces in `docker compose ps` and Komodo.

### Images and GHCR naming

- CI publishes `ghcr.io/<owner>/weaver-backend` and
  `ghcr.io/<owner>/weaver-frontend`, tagged `latest` (default branch), the
  semver version on a `v*` tag, and `sha-<commit>` on every push, via
  `docker/metadata-action`.
- **`github.repository_owner` keeps the account's casing, but image names
  must be lowercase.** Both publish jobs lowercase it (`${OWNER,,}`)
  first.
- **The image variables are required** rather than defaulting to a
  placeholder: the old `ghcr.io/OWNER/...` default failed with an unclear
  invalid-reference error. The deployment's compose file can't derive the
  owner the way CI does, so set them in `.env`.
- **Only web is containerised for development.** Android/iOS need an
  emulator or device a generic Linux container can't provide; they run
  natively and can point at the dockerised backend
  (`http://localhost:8080`, or `http://10.0.2.2:8080` from the Android
  emulator). A release APK can still be built in the pinned Flutter image,
  which includes the Android SDK.

## CI

`.github/workflows/docker-publish.yml` runs on every push and PR to
`master` and on `v*` tags:

- **`test-backend`:** `dotnet test backend/Weaver.sln` with coverage,
  uploaded as the `backend-coverage` artifact.
- **`test-frontend`:** `dart format --output=none --set-exit-if-changed lib
  test`, `flutter analyze`, `flutter test --coverage` (uploaded as
  `frontend-coverage`).
- **`publish-backend` / `publish-frontend`:** build the images on every
  run, so a broken Dockerfile fails the PR, but only log in and push when
  the event isn't a pull request.
- **Flutter is pinned to one version in three places**:
  `FLUTTER_VERSION` in the workflow (passed to the production Dockerfile's
  `ARG`), and the `ARG FLUTTER_VERSION` defaults in
  `docker/frontend/Dockerfile` and `docker/frontend/Dockerfile.dev`. It
  used to float on `stable`, so tests and the shipped build could use
  different SDKs, and the formatter's output depends on the SDK version.
  Bump all three together.

## Testing strategy

- **NUnit + Moq** for the backend (the project was scaffolded with xUnit
  and switched).
- **Moq only at real interface boundaries**: services are mocked in
  controller and MCP tool tests. `WeaverDbContext` is never mocked; service
  tests use the **EF Core InMemory provider**, which gives real
  create/read/update/delete semantics. Mocking `DbContext` / `DbSet` only
  re-asserts the mock's setup.
- **InMemory limits:** it doesn't enforce foreign keys, doesn't generate
  `xmin` or identity columns, and has no `text[]`, transaction or
  unique-index behaviour.
- **Integration tests cover what InMemory can't.**
  `Weaver.Api.IntegrationTests` runs `WebApplicationFactory<Program>`
  against a Testcontainers `postgres:16-alpine` in the `Production`
  environment: the auth pipeline, JSON enum serialisation, link and
  board-scope deletes, `WorkItem.Version` conflicts, `Number` generation,
  tags, rank ties, the recursive hierarchy queries, lookup reordering,
  startup configuration and MCP over HTTP. The refresh-token race was
  verified manually against Postgres.
- **Frontend:** view-model and repository unit tests with stub
  repositories and `MockClient`, including `Completer`-based concurrency
  tests for the shared refresh and for the board's overlapping loads and
  rollbacks; few widget tests.
- **Headless-browser checks** of the running web app need special
  handling because Flutter renders to a canvas; see
  [development.md](development.md#verifying-flutter-web-in-a-headless-browser).
