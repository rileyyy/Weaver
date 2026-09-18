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

https://sneekes.app/posts/building-my-own-kanban-self-hosted/

## Milestones

| Milestone | Status         | Result                            |
| --------- | -------------- | --------------------------------- |
| 0         | Done           | Architecture + repo + Docker      |
| 1         | Done           | Database + domain model           |
| 2         | Done           | API                               |
| 3         | Done           | Flutter shell                     |
| 4         | Done           | Standalone swimlane prototype     |
| 5         | Done           | API-connected board               |
| 6         | Done           | Hierarchy                         |
| 7         | Done           | Time-frame filter                 |
| 8         | Done           | Search/filter/sort                |
| 9         | In progress    | Work-item details                 |
| 10        | Done           | **Authentication**                |
| 11        | In progress    | Comments/links                    |
| 12        | Not started    | Attachments                       |
| 13        | Not started    | MCP                               |
| 14        | Not started    | Mobile refinement                 |
| 15        | Not started    | Production deployment             |

Implemented in dependency order (10, then 9, then 11) rather than numeric
order: Milestone 9's "assigned to" field requires Milestone 10's user
model to exist first. See "Open decisions" below for everything an agent
had to decide unattended while implementing these three — please review
before relying on this work.

## Open decisions — Milestones 9-11 (please review)

These were implemented overnight without the ability to ask; each is a
judgment call an agent made instead of you. Nothing here is final —
flag anything you'd rather have done differently and it can be changed.

**Conflicts with an existing decision, please look at this one first:**

- **`SprintId` was *not* added**, even though it was in the suggested
  field list. The "Time Filtering" section above records an explicit,
  previously-confirmed decision that this app has no fixed-length
  sprint/iteration concept — Milestone 7 deliberately replaced a
  "Sprint system" milestone with the arbitrary time-frame filter for
  exactly this reason. Adding `SprintId` now would directly contradict
  that recorded decision, so it was left out rather than guessed at. If
  you do want a sprint/iteration concept after all, that's a real design
  reversal worth deciding deliberately, not inferring from a field list.

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

- **Session strategy**: short-lived JWT access token (15 min) +
  longer-lived rotating refresh token (30 days). "Cache login so users
  don't have to keep signing in" is implemented as: the refresh token is
  stored in the Flutter app via `flutter_secure_storage` (OS keychain/
  keystore on native, encrypted storage on web); the access token is
  kept in memory only and silently re-issued from the refresh token on
  app start and on expiry. Logging out revokes the refresh token
  server-side, so a stolen/cached token stops working immediately rather
  than just expiring naturally later.
- **Password policy**: 12-character minimum, no forced
  uppercase/digit/symbol composition rules. This follows current NIST
  800-63B guidance (length matters far more than composition; forced
  composition rules push people toward predictable patterns). If you
  wanted the older-style composition requirements specifically, that's a
  quick change.
- **Passwords are hashed with `Microsoft.AspNetCore.Identity`'s
  `PasswordHasher<T>`** (PBKDF2-HMAC-SHA256, random salt per user,
  framework-managed iteration count) rather than a hand-rolled scheme or
  an extra third-party crypto dependency.
- **Basic brute-force lockout was added**: 5 consecutive failed logins
  locks the account for 15 minutes. This wasn't explicitly requested but
  falls under "typical best practices"; it's simple enough that leaving
  it out felt like the bigger judgment call.
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
- **CORS was narrowed** from the previous any-origin policy (which was
  explicitly left open only *until* auth existed — see `Program.cs`'s
  comment from Milestone 5) to a configured origin allowlist. Every
  board/work-item/user/comment/link endpoint now requires a valid access
  token (`[Authorize]`); only `/api/auth/*` stays anonymous.

**Comments and links (Milestone 11):**

- **"Links" means work-item-to-work-item relationships**, not external
  URLs and not file attachments (attachments is already its own,
  separate Milestone 12). A link is a flat, symmetric "related to"
  association between two work items — no relationship-type vocabulary
  (e.g. "blocks"/"duplicates"/"is caused by"). If you had something more
  structured in mind, this is the piece most likely to need rework.
- **Comments can only be edited/deleted by their own author.** There's no
  roles/admin concept yet to allow anyone else to moderate them.
