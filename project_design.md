# Project Development Rules

## Architecture

The application consists of:

- ASP.NET Core backend
- PostgreSQL database
- Flutter frontend
- Docker Compose deployment
- MCP integration implemented in C#

The application uses a layered architecture:

Domain
Application
Infrastructure
API

Business rules belong in the Application/Domain layers and must not
be implemented separately in controllers, Flutter widgets, or MCP tools.

## Work Items

Work items form a tree using ParentId.

Work items may have arbitrary depth.

A work item must never be allowed to become its own ancestor.

Changing a work item's status must not change its parent.

Changing a work item's parent is a separate operation from changing
its status.

## Board

The primary board is a swimlane board.

Each parent work item is represented by one swimlane.

Its direct children appear inside the swimlane.

Children may move between status columns.

Children must not be moved between swimlanes by normal status drag/drop.

## Time Filtering

This is not software-development project management, so there is no
fixed-length sprint/iteration concept.

Work items may optionally have a start date and an end date, set
independently of status and parent.

The board filters by an arbitrary, user-chosen time frame (a from/to
range), not a predefined cycle — the frame itself is not a saved entity.

A work item matches a time-frame filter if its own start/end window
overlaps the filter's. A work item missing either date is open-ended on
that side, not excluded — it is never treated as "unscheduled" for
filtering purposes.

## API

Prefer operation-oriented endpoints for state changes.

Do not expose EF entities directly from the API.

Use DTOs.

## MCP

MCP tools must call the same Application services used by the REST API.

MCP must never directly access EF Core.

Do not duplicate business logic between MCP and REST.

## Flutter

Business logic must not be placed in widgets.

Views use ViewModels/services.

The board must remain usable on both desktop/web and mobile.

## Testing

Every domain invariant requires an automated test.

Every state-changing application operation requires tests for:

- success
- invalid input
- authorization failure
- relevant business-rule violations

## Inspiration

Azure DevOps projet management (specifically sprint boards and heirarchy views)
and
https://sneekes.app/posts/building-my-own-kanban-self-hosted/

## Milestones

| Milestone | Status      | Result                        |
| --------- | ----------- | ----------------------------- |
| 0         | Done        | Architecture + repo + Docker  |
| 1         | Done        | Database + domain model       |
| 2         | Done        | API                           |
| 3         | Done        | Flutter shell                 |
| 4         | Done        | Standalone swimlane prototype |
| 5         | Done        | API-connected board           |
| 6         | Done        | Hierarchy                     |
| 7         | Done        | Time-frame filter             |
| 8         | Done        | Search/filter/sort            |
| 9         | Done        | Work-item details             |
| 10        | Done        | Authentication                |
| 11        | Done        | Comments/links                |
| 12        | Skip        | Attachments                   |
| 13        | Done        | MCP                           |
| 14        | Not started | Mobile refinement             |
| 15        | Not started | Production deployment         |

Implemented in dependency order (10, then 9, then 11) rather than numeric
order: Milestone 9's "assigned to" field requires Milestone 10's user
model to exist first. See "Open decisions" below for everything an agent
had to decide unattended while implementing these three — please review
before relying on this work.

## Open decisions — Milestones 9-11 (please review)

**Fields from the suggested list that map onto something that already exists:**

- **`DueDate` was not added as a new column.** `WorkItem.EndDate`
  (Milestone 7) already serves this purpose — it's the field the
  Milestone 8 sort option literally labels "Due date". A second field
  would just be a confusing duplicate.
- **`ProjectId` was not added as a stored column.** With the new
  `WorkItemLayer` concept (below), "which project is this under" is just
  "walk `ParentId` up to the nearest ancestor whose layer is Project" —
  storing it separately would duplicate the hierarchy and could drift out
  of sync with it if an item is ever reparented. If you want a project
  concept that's independent of the parent/child tree (e.g. items that
  can belong to a project without being its descendant), that's a
  different feature than what was asked for here and should be scoped
  separately.
- **`Type` became `LayerId`.** The field list named it `Type`, but the
  prose asked for configurable layers (epic → feature → task style,
  backed by a DB table). Both describe "what kind of item is this," so
  one field does both jobs: `WorkItemLayer` (`Id`, `Name`, `Order`),
  seeded with Project/Goal/Task, and a nullable `WorkItem.LayerId`. It's
  nullable and **unenforced against the parent/child tree on purpose** —
  you asked for parent/child to stay fluid, so a Task can still parent a
  Project if that's ever genuinely what someone wants; the layer is a
  label, not a constraint.
- `CreatedAt`/`UpdatedAt` already existed as `CreatedAtUtc`/`UpdatedAtUtc`
  — no change needed.

**Priority is a fixed enum, not a table.** Only layers were asked to be
configurable; Priority (`Low`/`Medium`/`High`/`Urgent`) follows the same
pattern as the existing `StatusCategory` enum instead. Say the word if
you'd rather it be admin-configurable like layers/statuses.

**Authentication design:**

- **Registration is open** (anyone can create an account with a
  username/password) — there's no admin/invite system yet to gate it
  with. Worth revisiting once this is anything other than a
  single-team/self-hosted tool.
- **`User.Kind` (`Human`/`Agent`) exists on the entity now**, per "note
  whether a user is a human or not," but the public registration
  endpoint always creates `Human` users — there's no way to self-register
  as an `Agent` yet, since you said that part "may come up later." An
  `Agent` user would need to be created some other way (direct DB
  insert, or a future admin/seeding path) until that's designed properly.
- **No password-reset/email flow exists.** No email field was requested
  and adding one felt like scope creep beyond "username/password" — but
  it means a forgotten password currently has no self-service recovery
  path. Flagging this as a known gap rather than silently deciding it
  doesn't matter.

**Comments and links (Milestone 11):**

- **"Links" means work-item-to-work-item relationships**, not external
  URLs and not file attachments (attachments is already its own,
  separate Milestone 12). A link is a flat, symmetric "related to"
  association between two work items — no relationship-type vocabulary
  (e.g. "blocks"/"duplicates"/"is caused by"). If you had something more
  structured in mind, this is the piece most likely to need rework.
- **Comments can only be edited/deleted by their own author.** There's no
  roles/admin concept yet to allow anyone else to moderate them.
- **Adding a link takes the target work item's raw id, not a title
  search.** There's no general "search all work items" endpoint to back
  a proper picker with (the board only ever fetches a specific scope's
  direct children) — building one felt like its own scoped feature
  rather than something to add silently as a side effect of this
  milestone. Worth a real search endpoint if the raw-id entry proves too
  rough in practice.

## Open decisions — Milestone 13 (MCP)

- **No new auth mechanism was built.** An MCP client authenticates exactly
  like any other API client: log in via `POST /api/auth/login` and send the
  resulting access token as `Authorization: Bearer <token>` when connecting.
  The MCP endpoint is mapped into the same ASP.NET Core pipeline as every
  controller (`app.MapMcp("/mcp")`, alongside `app.MapControllers()`), so it
  inherits the existing `[Authorize]` fallback policy for free. This means
  the `User.Kind` (`Human`/`Agent`) gap flagged back in Milestones 9-11 (no
  way to self-register as an `Agent`) is still unresolved — an MCP client
  today authenticates as whatever human account's credentials it's given.
  Fine for a personal AI assistant working on someone's own boards; worth a
  real look before any automated, non-human-operated agent needs its own
  identity.
- **`BoardsController`/`StatusesController`/`WorkItemLayersController`
  gained real service interfaces** (`IBoardService`/`IStatusService`/
  `IWorkItemLayerService`) as a prerequisite, not scope creep — they were
  the only controllers still querying `WeaverDbContext` directly, which made
  it impossible for MCP tools to expose those three resources without
  either duplicating that EF Core code or violating "MCP must never
  directly access EF Core." No REST behavior changed other than
  `BoardsController`'s invalid-`ScopeItemId` case, which now goes through
  the same `EntityNotFoundException` → `ApiExceptionMiddleware` path every
  other 404 in the API uses, instead of a bespoke plain-string body.
- **No `Weaver.Application` project was introduced**, even though the
  architecture section above describes a four-layer Domain/Application/
  Infrastructure/API split. The "Application" services already live in
  `Weaver.Infrastructure/Services` for every prior milestone, and MCP tools
  call those same interfaces the same way controllers do — moving them to a
  new project would be an unrelated, larger refactor than this milestone
  asked for.
- **MCP tools mirror the REST surface for work items, boards, statuses,
  work item layers, users, comments, and links — not auth.** Register/
  login/refresh/logout aren't exposed as tools; they're how a client gets
  the bearer token in the first place, not something an agent should be
  doing to itself mid-conversation. Attachments aren't covered either,
  since Milestone 12 was skipped.
- **Tool parameter/result enums (e.g. `WorkItemPriority`) serialize as
  their string name**, matching the REST API's own `JsonStringEnumConverter`
  convention (Milestone 10), via an explicit `JsonSerializerOptions` passed
  to `WithToolsFromAssembly` rather than the SDK's bare default (which
  otherwise renders `1` instead of `"Medium"`).
- **Domain exceptions are translated to `McpException` with the same
  message**, so an MCP client sees the same "Work item {id} was not
  found." text a REST caller gets in a 404 body, rather than the SDK's
  generic detail-free fallback for an unrecognized exception type. This is
  presentation-layer translation duplicated per-transport on purpose (REST
  has `ApiExceptionMiddleware`, MCP has its own equivalent), not a second
  copy of any business rule.
- **Hosted as a Streamable HTTP endpoint on the existing backend
  process/port (`/mcp`), stateless mode** — not a separate stdio process or
  standalone binary. Every MCP call is just an ordinary ASP.NET Core
  request: same DI scope per call, same JWT bearer auth, same Docker port
  already in place. No compose/Dockerfile changes were needed. A stdio-based
  MCP server (spawned as a subprocess by a desktop MCP client) would need
  its own distributable and was out of scope here.

## Open decisions — Frontend view improvements (pre-Milestone 14)

A round of frontend polish requested directly (not a numbered milestone):
swimlane assignee sizing/silhouette, click-an-avatar-to-assign everywhere,
Hierarchy view columns/headers/resizing, and a new Roadmap (formerly
"Calendar") timeline view. No backend changes were needed — `GET /users`
and `POST /work-items/{id}/assignee` already existed (Milestone 9) and
`WorkItemDto` already carried `Number`/`AssignedToUserId`/`StartDate`/
`EndDate`. Judgment calls made along the way:

- **The unassigned silhouette was added to the swimlane label (as asked)
  and to the Hierarchy view's new Assigned To column, but not to board
  cards.** A card's avatar still collapses to nothing when unassigned,
  matching its existing compact design (`AssigneeAvatar` gained a
  `showPlaceholderWhenUnassigned` flag rather than making this the new
  default everywhere). The Hierarchy column needed _some_ visible, tappable
  target in every row regardless of assignment state, or an unassigned
  item's row would have had nothing to click to assign it.
- **Hierarchy view: the expand/collapse caret and the new work-item-number
  column stay at a fixed horizontal position regardless of nesting depth —
  only the title's own text indents.** The previous single-status-column
  layout could get away with indenting the whole row (an `Expanded` title
  absorbed the padding, so the status column stayed flush against the
  right edge either way); once Status and Assigned To both became
  fixed-width, independently resizable columns after a fixed-width title,
  indenting the whole row would have shifted every column out of alignment
  with its own header as depth increased. Say the word if the caret itself
  was expected to indent alongside the title (the common file-tree
  convention) — that would need a different column-alignment approach.
- **Column widths (Hierarchy view's Number/Title/Status/Assigned To) are
  adjustable via draggable header dividers, but are pure view state** —
  like the existing swimlane/Hierarchy collapse sets, they reset on reload
  and aren't persisted per-user. Nothing in the request asked for
  persistence; worth a real look if users want their column layout to
  stick between sessions.
- **Roadmap is a hand-rolled Gantt-style widget, no new package
  dependency** — consistent with this codebase's existing preference for
  plain Flutter widgets over a package for a single screen's worth of UI
  (see the Flutter shell's `ViewModel` notes in `agent_notes.md`). It
  reuses `BoardViewModel.hierarchyRoots` directly, so it automatically
  gets the same sort/filter/search/status-visibility the Hierarchy view
  gets from the shared header, and the same collapse-tree/indentation
  behavior — its own timeframe (week/fortnight/month/quarter/year) and
  visible date window are separate, view-only state, deliberately not
  wired to the header's time-frame _filter_ (that filters which items
  show up at all; the roadmap's timeframe only changes the timeline's
  zoom level).
- **Quarter and Year timeframes use approximate 7-day/30-day units, not
  true calendar week/month boundaries.** Good enough for a zoomed-out
  overview; a work item's bar can be off by a day or two at that zoom
  level. Worth revisiting with real calendar-aware bucketing if that
  precision ever matters.
- **An item with neither a start nor an end date renders no bar at all in
  the Roadmap** — there's no natural place to put it on a timeline, the
  same reasoning the existing swimlane board already applies (no schedule
  label shown for an unscheduled card).
- **This machine's local Flutter SDK (3.32.6 / Dart 3.8.1) is out of date
  relative to what this project's `pubspec.yaml` and two pre-existing
  files need** — `flutter pub get` fails outright as committed
  (`flutter_secure_storage` ^11.2.0's `win32` dependency needs Dart
  ≥3.10), and even after a local-only `pubspec_overrides.yaml` workaround
  (never committed), `flutter analyze` surfaces real errors in
  `create_work_item_dialog.dart` and `work_item_detail_view.dart` from
  newer Material APIs (`RadioGroup`, `DropdownButtonFormField
.initialValue`) that don't exist in this older SDK — both pre-existing
  and unrelated to this round of changes. Every file touched by this round
  analyzes with zero errors, and the full board/repository test suite (77
  tests, including new coverage for `assign` and the Hierarchy item's
  `number`/`assignedToUserId` parsing) passes once pub resolution is
  unblocked — but the app couldn't be exercised end-to-end in a real
  browser this session as a result. Recommend `flutter upgrade` before the
  next round of frontend work.

## Open decisions — Tags (pre-Milestone 14 frontend round, continued)

Free-text tags were added to work items (small badges on the swimlane card,
Hierarchy, and Roadmap views; a text-based filter and search term), plus
the swimlane board card now shows the unassigned silhouette. This needed a
real backend field (a client-only tag would vanish on reload and never be
seen by a second user) — see `agent_notes.md`'s new "Tags" section for the
schema/validation decisions. Judgment calls specific to this round:

- **Tags are only ever edited from the work item detail dialog** — no
  inline "add a tag" affordance on a board card, Hierarchy row, or Roadmap
  row itself, unlike the assignee avatar (which is directly tappable
  everywhere it's shown). A tag list needed a small add/remove UI of its
  own (a text field + removable chips), which didn't have an obvious
  compact home on a card already showing a title, schedule, and avatar.
  Worth revisiting if quick inline tagging turns out to matter in practice.
- **The tag filter (in the existing Filters dialog) is "any selected tag
  matches"** (OR), not "all selected tags must be present" (AND) — the
  more common tag-filter convention, and simpler to reason about
  alongside the existing status-column filter. An empty selection applies
  no tag filter at all, rather than needing a separate "show all" toggle.
- **The Roadmap view's title/tags split within its fixed 260px left column
  is a fixed 3:2 flex ratio, not true "however much space the title
  doesn't need."** True packing would need two-pass intrinsic-width
  measurement (Flutter's `Flex` doesn't redistribute unused space from a
  shrunk sibling in one layout pass); a fixed ratio is simpler, still
  ellipsizes a long title and still overflow-badges tags that don't fit
  their share, and behaves predictably as titles/tag counts vary. Revisit
  if that column ever becomes independently resizable like Hierarchy's
  columns already are.
- **Tag matching (search, filter, and de-duplication) is
  case-insensitive**, but a tag's *displayed* casing is whatever was typed
  first — "Urgent" and "urgent" collapse to one badge, keeping the first
  spelling. No tag rename/merge UI exists if someone wants to fix a
  casing/spelling inconsistency after the fact beyond deleting and
  re-adding it.
