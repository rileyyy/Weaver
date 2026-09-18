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
| 5         | Done           | **API-connected board**           |
| 6         | Not started    | Hierarchy                         |
| 7         | Not started    | Sprint system                     |
| 8         | Not started    | Search/filter/sort                |
| 9         | Not started    | Work-item details                 |
| 10        | Not started    | Authentication                    |
| 11        | Not started    | Comments/links                    |
| 12        | Not started    | Attachments                       |
| 13        | Not started    | MCP                               |
| 14        | Not started    | Mobile refinement                 |
| 15        | Not started    | Production deployment             |
