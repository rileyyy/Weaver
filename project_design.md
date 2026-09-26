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

There is no separate `Weaver.Application` project yet: the Application
services (`IWorkItemService` etc.) currently live in
`Weaver.Infrastructure/Services`. Treat that folder as the Application
layer until it's split out (see B-M3 in `code_review_findings.md`).

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

## History and decisions

These rules are the constraints; the rest lives elsewhere.
[CHANGELOG.md](CHANGELOG.md) has the milestone status and what shipped,
[docs/architecture.md](docs/architecture.md) records the design decisions
made along the way and why, and
[docs/open-questions.md](docs/open-questions.md) lists the decisions that
are still open.
