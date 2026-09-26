# Changelog

What shipped, newest first. Design reasoning lives in
[docs/architecture.md](docs/architecture.md); open items in
[docs/open-questions.md](docs/open-questions.md).

## Milestone status

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
| 12        | Skipped     | Attachments                   |
| 13        | Done        | MCP                           |
| 14        | Not started | Mobile refinement             |
| 15        | Not started | Production deployment         |

Milestones 9–11 were built in dependency order (10, then 9, then 11),
because Milestone 9's "assigned to" field needs Milestone 10's users.

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
