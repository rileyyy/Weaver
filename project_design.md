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
| 13        | Not started | MCP                           |
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
