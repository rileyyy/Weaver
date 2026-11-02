# Project Development Rules

## Architecture

The application consists of:

- ASP.NET Core backend
- PostgreSQL database
- Flutter frontend
- Published container images, with an example Docker Compose deployment
  ([ADR-0008](docs/adr/0008-images-first-deployment.md))
- MCP integration implemented in C#

The application uses a layered architecture:

Domain
Application
Infrastructure
API

Business rules belong in the Application/Domain layers and must not
be implemented separately in controllers, Flutter widgets, or MCP tools.

The Application services (`IWorkItemService` etc.) live in
`Weaver.Application`, which depends only on Domain and on EF Core's API
through `IWeaverDbContext`. Infrastructure implements that interface and
owns the provider, model configuration, migrations and JWT signing.

## Work Items

Work items form a tree using ParentId.

Work items may have arbitrary depth.

A work item must never be allowed to become its own ancestor.

Changing a work item's status must not change its parent.

Changing a work item's parent is a separate operation from changing
its status.

*From Milestone 16:* field updates use patch semantics: an omitted field
is unchanged and an explicit null clears it. Every write accepts an
optional expected version.

*From Milestone 16:* deleting a work item moves it and its subtree to the Trash; only a purge
removes it permanently. ([ADR-0007](docs/adr/0007-trash.md))

## Spaces and Types

*Applies from Milestone 16; see [ADR-0001](docs/adr/0001-spaces.md) and
[ADR-0002](docs/adr/0002-types-and-custom-fields.md).*

Every work item belongs to exactly one Space. A Space holds a forest of
work-item trees.

Statuses, priorities, Types and their custom fields belong to a Space,
and are configured by users, not in code.

Reparenting never crosses a Space boundary. Moving a work item to another
Space is a separate, explicit operation that maps its status, Type and
field values.

Types do not constrain which Types may nest under which.

## Repeating Work Items

A work item may repeat on a schedule. The item is the template; each
occurrence is a new work item created under the template's current
parent, so earlier occurrences remain for reference.

The schedule is stored separately from the work item.

An occurrence must never be created twice for the same date, and a
deleted occurrence must not be recreated by routine generation.

A generated occurrence must not itself be given a schedule.

## Board

The primary board is a swimlane board. *From Milestone 16* it is a
layout of a View (a saved query over one or more Spaces), not a stored
entity of its own.

Each parent work item is represented by one swimlane.

Its direct children appear inside the swimlane.

Children may move between status columns.

Children must not be moved between swimlanes by normal status drag/drop.

## Time Filtering

This is not software-development project management, so there is no
sprint/iteration concept. Intent about timing is a free-text When,
resolved to dates and/or blocked-by links. ([ADR-0003](docs/adr/0003-when-and-backlog.md))

Work items may optionally have a start date and an end date, set
independently of status and parent.

The board filters by an arbitrary, user-chosen time frame (a from/to
range), not a predefined cycle.

A work item with at least one date matches a time-frame filter if its
start/end window overlaps the filter's; a single missing date is
open-ended on that side.

*From Milestone 16:* a work item with no dates at all is in the Backlog
and does not match any time frame. A parent with no dates of its own is
still shown when any of its children match.

Weaver flags a dependency conflict (a blocked item starting before its
blocker ends) but never reschedules anything itself.

## API

Prefer operation-oriented endpoints for state changes.

Do not expose EF entities directly from the API.

Use DTOs.

## MCP

MCP tools must call the same Application services used by the REST API.

MCP must never directly access EF Core.

Do not duplicate business logic between MCP and REST.

Filtering and search are Application-layer queries, never client-side
logic. ([ADR-0006](docs/adr/0006-server-side-query-and-views.md))

## History and Agents

*Applies from Milestone 16; see [ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)
and [ADR-0005](docs/adr/0005-change-history.md).*

Every change to a work item is recorded in an append-only history with
the account, API key and actor label that made it. Application services
write the history, so REST and MCP cannot diverge.

Agents authenticate as agent accounts with scoped API keys, never with a
human's password.

Agents may read admin data (server settings, lookups, Types, accounts,
privileges) but never change it. Agents may delete work items and restore
them from the Trash, but never purge it.

MCP responses stay lean: lists return summaries, and full detail is
opt-in.

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

Azure DevOps project management (specifically sprint boards and hierarchy views)
and
https://sneekes.app/posts/building-my-own-kanban-self-hosted/

## History and decisions

These rules are the constraints; the rest lives elsewhere.
[CHANGELOG.md](CHANGELOG.md) has the milestone status and what shipped,
[docs/architecture.md](docs/architecture.md) records the design decisions
made along the way and why,
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md) and
[docs/adr/](docs/adr/) plan Milestones 16–20, and
[docs/open-questions.md](docs/open-questions.md) lists the decisions that
are still open.
