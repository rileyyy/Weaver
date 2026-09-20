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
