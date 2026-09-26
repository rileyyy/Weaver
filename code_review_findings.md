# Weaver: Code & Architecture Review

**Date:** 2026-09-25 · **Revision reviewed:** `master` @ `55dd9ce`
**Scope:** `backend/` (.NET 9 API, Domain, Infrastructure, tests), `frontend/` (Flutter app + tests), `docker/`, `compose*.yaml`, `.github/workflows/`.
No code was changed as part of this review.

Each finding has a checkbox. Tick it (`[x]`) in the commit that fixes it, and note the branch next to it, so the tick lands on `master` together with the fix.

Severity key: **High** means data loss, a security exposure, or a bug users will regularly hit. **Medium** means a real defect or a design problem that will get more expensive the longer it stays. **Low** means cleanup or consistency work.

---

## Summary

The codebase is in better shape than most at this stage:

- Layering is clear (Domain → Infrastructure → Api).
- Error handling is centralised.
- Auth is secure by default (fallback policy).
- Optimistic concurrency is set up on work items.
- `flutter analyze` is clean.
- Unit-test coverage of single-call paths is good.

The most important problems cluster in four areas:

1. **Concurrency on the client.** Parallel requests trigger parallel token refreshes, which log the user out. Scope loads race, and optimistic rollbacks restore stale snapshots.
2. **`BoardViewModel.retry()` is used as "refresh"**, but it replays the last action, which can re-create a work item.
3. **Configuration and deployment safety.** A hard-coded JWT key in `appsettings.json` defeats the startup "no key configured" check. The API port is published directly with no TLS. `/mcp` is not proxied.
4. **Referential-integrity gaps the tests can't see.** Deleting a linked work item or a board's scope item fails with a 500 in Postgres. The EF InMemory test provider hides this.

### Top 10 to fix first

| # | Finding | Area |
|---|---------|------|
| 1 | [F-H1](#f-h1-parallel-requests-trigger-concurrent-token-refreshes-and-log-the-user-out) Concurrent token refresh logs users out | Frontend auth |
| 2 | [F-H2](#f-h2-retry-is-used-as-refresh-and-replays-the-last-action-duplicate-work-items) `retry()` replays `createWorkItem` → duplicate items | Frontend board |
| 3 | [B-H1](#b-h1-appsettingsjson-ships-a-jwt-signing-key-which-defeats-the-startup-guard) Committed JWT signing key defeats the startup guard | Backend config |
| 4 | [B-H2](#b-h2-deleting-a-linked-work-item-or-a-boards-scope-item-returns-500) Deleting a linked / scope work item → 500 | Backend data |
| 5 | [F-H5](#f-h5-schedule-dates-display-one-day-early-east-of-utc) Dates show one day early east of UTC | Frontend + API contract |
| 6 | [B-H3](#b-h3-optimistic-concurrency-cannot-prevent-lost-updates-between-clients) Row version never reaches clients → lost updates | Backend API |
| 7 | [I-H1](#i-h1-production-compose-publishes-the-api-directly-over-plain-http-and-mcp-isnt-proxied) API exposed on :8080 over plain HTTP, `/mcp` not proxied | Deployment |
| 8 | [F-H3](#f-h3-optimistic-rollbacks-restore-whole-list-snapshots)/[F-H4](#f-h4-scope-loads-race-and-an-older-response-can-overwrite-a-newer-one) Rollback snapshots and scope-load races | Frontend board |
| 9 | [B-M1](#b-m1-no-request-validation-so-oversized-input-returns-500) No input validation; oversized input → 500 | Backend API |
| 10 | [T-1](#t-1-no-integration-tests-against-a-real-pipeline-or-database) No integration tests against real Postgres / HTTP pipeline | Testing |

---

## 1. Backend (.NET)

### High

#### B-H1. `appsettings.json` ships a JWT signing key, which defeats the startup guard
- [ ] **Resolved** — *Deferred: the committed key is dev-only and not used by any production deployment yet. Revisit before Milestone 15 (production deployment).*
- **Where:** [appsettings.json:12-14](backend/src/Weaver.Api/appsettings.json#L12-L14), [Program.cs:56-61](backend/src/Weaver.Api/Program.cs#L56-L61), [JwtOptions.cs:3-9](backend/src/Weaver.Infrastructure/Auth/JwtOptions.cs#L3-L9)
- **Issue:** `Program.cs` throws if `Jwt:SigningKey` is empty, and the `JwtOptions` doc says the key "has no default and startup should fail loudly without one". But `appsettings.json`, which loads in every environment, contains `insecure-development-only-signing-key-do-not-use-in-production`. So the guard can never fire. Any deployment that forgets `Jwt__SigningKey` (for example, running the image outside `compose.yaml`) silently signs tokens with a key that is public in the repo, and anyone can forge a token for any user. The same file also ships a default DB password.
- **Fix:**
  - Move the dev key and connection string to `appsettings.Development.json` (or user-secrets). `compose.override.yaml` already sets both.
  - Also reject keys shorter than 32 bytes (HS256 minimum) and the known placeholder value when not in Development.

#### B-H2. Deleting a linked work item or a board's scope item returns 500
- [x] **Resolved** in `bugfix/delete-linked-work-item`: links are removed with the deleted items, and deleting a board's scope item returns 409. Checked by hand against real Postgres; the automated real-Postgres test is still pending [T-1](#t-1-no-integration-tests-against-a-real-pipeline-or-database).
- **Where:** [WorkItemLinkConfiguration.cs:18-26](backend/src/Weaver.Infrastructure/Configurations/WorkItemLinkConfiguration.cs#L18-L26), [BoardConfiguration.cs:13-16](backend/src/Weaver.Infrastructure/Configurations/BoardConfiguration.cs#L13-L16), [WorkItemService.cs:220-234](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L220-L234), [ApiExceptionMiddleware.cs](backend/src/Weaver.Api/Middleware/ApiExceptionMiddleware.cs)
- **Issue:** Both `WorkItemLink` FKs and `Board.ScopeItemId` are `Restrict`. `DeleteAsync` removes neither links nor boards first, so Postgres rejects the delete with a `DbUpdateException`. The middleware doesn't map that exception, so the client gets an opaque 500. A cascade delete fails entirely if *any* descendant has a link. `agent_notes.md:659-668` acknowledges the restriction but not the 500. The InMemory provider used in tests doesn't enforce these FKs, so no test catches it.
- **Fix:** Decide on the behaviour first:
  - either remove the links (and clear or reject scoped boards) inside `DeleteAsync`,
  - or throw a domain exception (for example `WorkItemHasLinksException` → 409).
  Then add a test against real Postgres (see [T-1](#t-1-no-integration-tests-against-a-real-pipeline-or-database)).

#### B-H3. Optimistic concurrency cannot prevent lost updates between clients
- [x] **Resolved** in `bugfix/work-item-lost-updates`: `Version` is in `WorkItemDto`. Details, schedule and tags accept `ExpectedVersion` and return 409 when it's stale. The detail dialog sends it, and after a conflict it reloads the item and re-seeds its fields. Status, parent and assignee are deliberately last-write-wins. **Remaining gap:** the board's own reschedule/tag calls don't send a version yet, because board cards don't track one; that belongs with [F-H6](#f-h6-board-hierarchy-and-roadmap-go-stale-after-edits)'s shared item store.
- **Where:** [WorkItemConfiguration.cs:50-53](backend/src/Weaver.Infrastructure/Configurations/WorkItemConfiguration.cs#L50-L53), [WorkItemDto.cs:5-20](backend/src/Weaver.Api/Contracts/WorkItemDto.cs#L5-L20), every mutating method in [WorkItemService.cs](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs)
- **Issue:** `xmin` is mapped as a row version, but `Version` is never exposed in `WorkItemDto` and no request accepts an expected version. Each request reads the current row and saves it straight back, so the concurrency token only covers the few milliseconds inside one request. User A's `PUT /details` silently overwrites user B's edit made seconds earlier. The middleware's message "changed by someone else. Refresh and try again" is effectively unreachable.
- **Fix:** Return `Version` in the DTO and accept it on mutating requests (or use `If-Match`/ETag). Set `Entry(item).Property(w => w.Version).OriginalValue = expected` before `SaveChanges`. The frontend will need to carry it too.

#### B-H4. Refresh-token rotation has no reuse detection and a double-spend race
- [x] **Resolved** in `bugfix/refresh-token-reuse-detection`:
  - Reusing a rotated token revokes every token issued from it (only that session's chain).
  - `RefreshToken` has an `xmin` concurrency token.
  - The lifetime comes from `JwtOptions`.
  - Expired tokens are purged per user on login/refresh.
  - A 30 s grace window stops the current client stampede ([F-H1](#f-h1-parallel-requests-trigger-concurrent-token-refreshes-and-log-the-user-out)) from triggering revocation.
- **Where:** [AuthService.cs:95-111, 137-160](backend/src/Weaver.Infrastructure/Services/AuthService.cs#L95-L111), [RefreshTokenConfiguration.cs](backend/src/Weaver.Infrastructure/Configurations/RefreshTokenConfiguration.cs)
- **Issues:**
  - `ReplacedByTokenHash` is written but never read. Presenting an already-rotated token (the classic sign of a stolen token) just returns 401 instead of revoking the whole token family.
  - `RefreshToken` has no concurrency token. Two simultaneous refreshes with the same token both pass `IsActive` and both mint new pairs.
  - `JwtOptions.RefreshTokenLifetime` exists but is ignored: [AuthService.cs:148](backend/src/Weaver.Infrastructure/Services/AuthService.cs#L148) hard-codes `AddDays(30)`.
  - Expired and revoked tokens are never purged.
- **Fix:**
  - Add a concurrency token (`xmin`) to `RefreshToken`, or do a conditional `UPDATE … WHERE RevokedAtUtc IS NULL`.
  - On reuse of a revoked token, revoke all of that user's active tokens.
  - Inject `IOptions<JwtOptions>` and use `RefreshTokenLifetime`.
  - Add a periodic cleanup.
- **Note:** This interacts with [F-H1](#f-h1-parallel-requests-trigger-concurrent-token-refreshes-and-log-the-user-out). Fix the client stampede first, or strict reuse detection will log users out even more often.

### Medium

#### B-M1. No request validation, so oversized input returns 500
- [x] **Resolved** in `bugfix/request-validation`: titles, comment bodies and board names are required, non-blank and length-checked. Tags are capped at 20 per item and 50 characters each. Failures return 400 via `DomainValidationException` in both REST and MCP. Max lengths are shared constants on the entities, also used by the EF configurations.
- **Where:** all `Contracts/*Request` records, [WorkItemService.cs](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs), [CommentService.cs](backend/src/Weaver.Infrastructure/Services/CommentService.cs), [BoardService.cs](backend/src/Weaver.Infrastructure/Services/BoardService.cs)
- **Issue:** Nothing checks length or blankness:
  - Titles over 500 characters, comments over 4000 and board names over 200 hit the DB constraint and return a `DbUpdateException` → 500.
  - Empty or whitespace titles and comment bodies are accepted.
  - Tag count and tag length are unbounded (`text[]`).
  The MCP tools call the same services, so agents can hit these too.
- **Fix:** Validate in the service or domain layer (so REST and MCP share it) and throw a typed domain exception → 400. Share the max-length constants between validation and the EF configurations.

#### B-M2. Anemic domain model: invariants live in services, and every setter is public
- [ ] **Resolved**
- **Where:** [WorkItem.cs](backend/src/Weaver.Domain/WorkItem.cs), [WorkItemService.cs:127-218](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L127-L218)
- **Issue:** Rules such as "start ≤ end", tag normalisation, and "status change never reparents" are enforced in `WorkItemService`, not on `WorkItem`. Any code with a `WorkItem` can bypass them (`item.StartDate = …`). This also forces every rule test through EF.
- **Fix:** Move behaviour onto the entity (`item.Reschedule(start, end, clock)`, `item.SetTags(tags)`, `item.MoveTo(status, rank)`) with private setters. Services then orchestrate loading and saving, and the rules become plain unit tests in `Weaver.Domain.Tests`, which today only covers `RankCalculator`.

#### B-M3. "Application" services live in Infrastructure and depend on the concrete `DbContext`
- [ ] **Resolved**
- **Where:** `backend/src/Weaver.Infrastructure/Services/*`
- **Issue:** Business orchestration (cycle detection, rank computation, auth lockout) is in the Infrastructure project and uses `WeaverDbContext` directly. `McpExceptionTranslation`'s doc even refers to "the Application services", a layer that doesn't exist. As a result, services can only be tested with an EF provider, and the provider in use (InMemory) behaves differently from Postgres ([T-1](#t-1-no-integration-tests-against-a-real-pipeline-or-database)).
- **Fix (incremental):** Add a `Weaver.Application` project for services and interfaces, and keep Infrastructure for EF, JWT and configuration. The `IXxxService` interfaces already exist, so this is mostly a file move. Don't hide EF behind a generic repository unless there's a concrete need.

#### B-M4. The exception-to-status mapping is duplicated in two hand-maintained lists
- [ ] **Resolved**
- **Where:** [ApiExceptionMiddleware.cs:26-84](backend/src/Weaver.Api/Middleware/ApiExceptionMiddleware.cs#L26-L84), [McpExceptionTranslation.cs:40-48](backend/src/Weaver.Api/Mcp/McpExceptionTranslation.cs#L40-L48)
- **Issue:** Adding a domain exception means editing a 14-branch `catch` chain *and* a separate `is … or …` list, and the two already differ (MCP lacks `DbUpdateConcurrencyException`). This violates Open/Closed.
- **Fix:** Introduce an abstract `DomainException` with a `Kind` (NotFound, Conflict, Validation, Forbidden, Unauthorized). Map `Kind` to a status in one place, and translate all `DomainException`s in MCP. Consider `IExceptionHandler` + `AddProblemDetails()` (built into .NET 8+), which also sets `application/problem+json` correctly; the current `WriteAsJsonAsync` sends `application/json`.

#### B-M5. Unmapped framework exceptions leak as 500
- [ ] **Resolved**
- **Issue:** Several ordinary failures surface as 500:
  - `ClaimsPrincipalExtensions.GetUserId` uses `Guid.Parse` and throws `FormatException` on a malformed `sub` ([ClaimsPrincipalExtensions.cs:16-18](backend/src/Weaver.Api/Auth/ClaimsPrincipalExtensions.cs#L16-L18)).
  - `RankCalculator` throws `ArgumentException` if two siblings share a rank, which concurrent inserts at the same position can produce ([RankCalculator.cs:31-36](backend/src/Weaver.Domain/RankCalculator.cs#L31-L36)).
  - Unique-index violations (e.g. two concurrent registrations of the same username) surface as `DbUpdateException`.
- **Fix:** Use `Guid.TryParse` and reject with 401. Handle unique violations (`PostgresException.SqlState == "23505"`) as 409. Make ranks robust to ties: rebalance the cell when `previous >= next`.

#### B-M6. Auth hardening
- [ ] **Resolved**
- **Where:** [AuthController.cs](backend/src/Weaver.Api/Controllers/AuthController.cs), [AuthService.cs:58-93, 126-135](backend/src/Weaver.Infrastructure/Services/AuthService.cs#L58-L93)
- **Issues:**
  - **Open self-registration:** anyone who can reach the API can create an account and then read and modify every work item. There is no per-board or per-tenant authorization.
  - **Lockout as a DoS vector:** 5 wrong guesses lock any account for 15 minutes, and there is no rate limiting on `/api/auth/*`.
  - **Timing oracle:** the unknown-user path skips password hashing, so response time reveals whether a username exists. Registration reveals it directly anyway via `UsernameTakenException`.
  - `FailedLoginAttempts` isn't reset when a lockout expires, so the first wrong password afterwards re-locks the account immediately.
  - `UserKind.Agent` exists, but there is no way to create or authenticate agent users (MCP clients must use a human password login).
- **Fix:**
  - Gate registration behind config (invite or admin).
  - Add `AddRateLimiter` on the auth endpoints.
  - Verify against a dummy hash when the user is unknown.
  - Reset the counter on lockout expiry.
  - Plan an API-key or service-account flow for agents.

#### B-M7. Unbounded and N+1 queries
- [ ] **Resolved** — *partly: the board's per-lane N+1 is gone (F-M9), and the child/all/swimlane queries have a deterministic `Rank, Number` order. Paging, `AsNoTracking` elsewhere and the ancestor/descendant walks are still open.*
- **Where:** [WorkItemService.cs:25-28](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L25-L28) (`GetAllAsync`, no paging), [WorkItemService.cs:245-286](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L245-L286) (one query per ancestor level / descendant level), [WorkItemService.cs:301](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L301) (whole cell loaded to compute one rank), [BoardService.cs:16-17](backend/src/Weaver.Infrastructure/Services/BoardService.cs#L16-L17) (no ordering)
- **Issue:** Fine at today's scale, but the frontend fetches `/work-items/all` for Hierarchy and Roadmap. The code comment "revisit if item counts grow" has no metric attached.
- **Fix:**
  - Use `AsNoTracking()` on reads.
  - Load only the two neighbouring ranks (`Where(Rank > after.Rank).OrderBy(Rank).Take(1)`).
  - Use a recursive CTE for ancestors and descendants once real-Postgres tests exist.
  - Add paging or scoping to `/all`.
  - Give `GetAllAsync` a deterministic order.

#### B-M8. Startup auto-migration and hosting details
- [ ] **Resolved** — *partly: forwarded headers and dropping `UseHttpsRedirection` were done with I-H1, and `/health` with I-M1. Moving migrations out of startup is still open.*
- **Where:** [Program.cs:107-110, 120](backend/src/Weaver.Api/Program.cs#L107-L110)
- **Issues:**
  - `MigrateAsync()` on every boot races if more than one replica starts, and it applies schema changes without a deploy gate.
  - `UseHttpsRedirection()` runs behind nginx with no HTTPS port configured and no `UseForwardedHeaders()`, so it logs a warning and does nothing useful. `X-Forwarded-Proto` from nginx is ignored.
  - There are no health endpoints for compose or orchestration.
- **Fix:**
  - Move migrations to an explicit step (a `migrate` command or a bundle), or keep them behind a config flag.
  - Add `UseForwardedHeaders` and drop HTTPS redirection in-container.
  - Add `MapHealthChecks("/health")` with a DB check and wire it into `compose.yaml`.

### Low

- [ ] **B-L1. `UpdatedAtUtc` is set by hand in 8 places, and `DateTimeOffset.UtcNow` is called directly everywhere.** Use a `SaveChanges` interceptor for timestamps and inject `TimeProvider` so the lockout, expiry and schedule tests don't depend on wall-clock time.
- [ ] **B-L2. Inconsistent REST responses.**
  - `POST` comments and links return `200 Ok` while work items and boards return `201 CreatedAtAction` ([CommentsController.cs:29](backend/src/Weaver.Api/Controllers/CommentsController.cs#L29), [WorkItemLinksController.cs:28](backend/src/Weaver.Api/Controllers/WorkItemLinksController.cs#L28)).
  - Mutations use `POST /{id}/status` etc. rather than `PATCH`/`PUT`.
  - The controllers have no `[ProducesResponseType]`, so the OpenAPI doc lacks error shapes.
- [ ] **B-L3. Multiple types per file** (against CLAUDE.md §2.4):
  - `Status.cs` (+`StatusCategory`), `User.cs` (+`UserKind`), `WorkItem.cs` (+`WorkItemPriority`)
  - `AuthDto.cs` (6 records), `WorkItemDto.cs` (+`WorkItemLayerDto` and 7 request records)
  - `IJwtTokenService.cs` (+`AccessToken`), `IAuthService.cs` (+`AuthResult`)
- [ ] **B-L4. Leftover scaffold.**
  - [Weaver.Api.http](backend/src/Weaver.Api/Weaver.Api.http) still targets `/weatherforecast/`.
  - `Program.cs:17` has the "Add services to the container." template comment.
  - The DI registrations could become an `AddWeaverInfrastructure()` extension in Infrastructure, keeping `Program.cs` focused on the pipeline.
- [ ] **B-L5. `CommentDto` hides a missing author** by returning `AuthorUsername = ""` ([CommentDto.cs:18](backend/src/Weaver.Api/Contracts/CommentDto.cs#L18)). `WorkItemLinkDto` uses `!` on navigation properties that callers must remember to `Include` ([WorkItemLinkDto.cs:14](backend/src/Weaver.Api/Contracts/WorkItemLinkDto.cs#L14)). Project straight to DTOs in the query instead.
- [ ] **B-L6. Package versions are inconsistent.** `Microsoft.AspNetCore.OpenApi` is 9.0.7, `JwtBearer`/`Identity.Core` are 9.0.9, and EF is 9.0.20. Consider `Directory.Packages.props` (central package management) so one bump updates them all.
- [ ] **B-L7. `WouldCreateCycleAsync` returns `false` when it detects an *existing* cycle** ([WorkItemService.cs:256-259](backend/src/Weaver.Infrastructure/Services/WorkItemService.cs#L256-L259)). That state should be impossible, but reporting "no cycle" hides corruption. Throw or log instead.

---

## 2. Frontend (Flutter)

`flutter analyze` is clean, so everything below is logic, design or project-rule level.

### High

#### F-H1. Parallel requests trigger concurrent token refreshes and log the user out
- [x] **Resolved** in `bugfix/token-refresh-stampede`: concurrent callers share one in-flight refresh. A failed refresh only clears the session if it's still the one the refresh started from. Covered by `Completer`-based tests.
- **Where:** [auth_session_store.dart:60-74](frontend/lib/features/auth/data/auth_session_store.dart#L60-L74), `auth_http_client.dart:24`
- **Issue:** `ensureValidSession()` has no in-flight guard. `WorkItemDetailViewModel.load` sends 7 requests in parallel, and `loadBoard` sends one per swimlane. When the access token has expired, every request calls `refresh(sameToken)`. The backend rotates the token, so the first call succeeds and the rest get 401. Each failure then runs `clear()`, which wipes the session the first call just stored. In practice, the first board load or detail open after the access token expires (15 minutes) sends the user back to the login screen.
- **Fix:** Share one refresh `Future` (`_refreshing ??= _doRefresh().whenComplete(() => _refreshing = null)`). Only `clear()` if the session is still the one the failed refresh started from. Add a test where concurrent sends produce exactly one `refresh` call.

#### F-H2. `retry()` is used as "refresh" and replays the last action: duplicate work items
- [x] **Resolved** in `bugfix/board-retry-replays-create`: `_retry` is only recorded on failure. A new `refreshCurrentScope()` replaces `retry()` as the post-delete refresh. `createWorkItem` no longer goes through `_changeScope`: a failure keeps the board and shows a SnackBar.
- **Where:** [board_view_model.dart:252, 463-477, 648-655](frontend/lib/features/board/board_view_model.dart#L648-L655), [board_view.dart:107-110](frontend/lib/features/board/board_view.dart#L107-L110)
- **Issue:** `_changeScope` stores `_retry` on *every* call, including successful ones, and `createWorkItem` goes through `_changeScope`. `BoardView._onWorkItemDeleted` calls `retry()` to refresh the board. So "create item X, then delete any item from the detail dialog" POSTs X again. If the last action was `drillInto`, the breadcrumb is pushed a second time ("Board > X > X").
- **Fix:**
  - Add `refreshCurrentScope()`, which reloads `_breadcrumbs.last.id`.
  - Only set `_retry` when the action fails.
  - Take `createWorkItem` out of `_changeScope`. A failed create shouldn't replace the board with an error screen, and a retry must never re-POST.

#### F-H3. Optimistic rollbacks restore whole-list snapshots
- [x] **Resolved** in `bugfix/board-state-races`: all five board mutations go through one `_optimistic` helper, which reverts only the changed item by id and skips the revert if the data was reloaded in the meantime. The detail dialog's tag rollback re-seeds from the last server-confirmed item.
- **Where:** `board_view_model.dart:299-440` (five copies), `work_item_detail_view.dart:534-539`
- **Issue:** Each mutation captures `previousSwimlanes = _swimlanes` and restores it on failure. If move A fails after move B succeeded, the rollback reverts B too. If a scope change finished in between, the rollback writes the previous scope's lanes under the new breadcrumbs.
- **Fix:** Roll back per item (reapply the inverse change to the one card), and drop the rollback if the scope generation has changed. Pull the five copies into a single `_optimistic(apply, persist, revert, message)` helper.

#### F-H4. Scope loads race, and an older response can overwrite a newer one
- [x] **Resolved** in `bugfix/board-state-races`: scope and hierarchy loads carry a generation number. A load only applies its result, and only clears the spinner, if it's still the newest.
- **Where:** `board_view_model.dart:159-174, 234-279, 648-665`
- **Issue:** Nothing identifies the latest request. Clicking a breadcrumb and then quickly drilling into a card starts two loads, and whichever finishes last wins. The breadcrumbs and lanes can then describe different scopes, and the first completion hides the spinner while the second is still running. `loadHierarchy` has the same problem.
- **Fix:** Capture a monotonically increasing request id and discard stale completions, including their `finally` state resets.

#### F-H5. Schedule dates display one day early east of UTC
- [x] **Resolved** in `bugfix/schedule-date-timezone`: `StartDate`/`EndDate` are `DateOnly` (`date` column), sent as `yyyy-MM-dd`. The migration rounds existing values to the nearest UTC midnight, which recovers the picked day. The frontend parses calendar dates as local midnight and all other timestamps with `.toLocal()`. The full suite passes under UTC+2, UTC+9 and UTC−7. F-M13/F-M14 followed in `bugfix/time-filter-and-roadmap-dates`.
- **Where:** `api_board_repository.dart:94-95, 200-201`, `api_work_item_detail_repository.dart:125-126, 233`, `widgets/date_format.dart:3-8`
- **Issue:** The date picker returns local midnight, and the client sends it `toUtc()` (for example `2026-09-24T22:00Z` for 25 Sept in UTC+2). The value comes back from the API and is parsed as UTC, with no `.toLocal()`. `formatDate` then prints 24 Sept. Picker initial dates, comment timestamps and Created/Updated are also shown in UTC. The team works in UTC+2, so this affects them directly.
- **Fix:** Choose a convention. The recommendation is to treat schedule dates as calendar dates: send `yyyy-MM-dd` and make the backend `StartDate`/`EndDate` a `DateOnly`. Parse and display all other timestamps through a single `parseApiDate(...).toLocal()`. Add a round-trip test in a non-UTC zone.

#### F-H6. Board, Hierarchy and Roadmap go stale after edits
- [x] **Resolved** (short-term fix) in `bugfix/board-stale-after-edits`: `showWorkItemDetailDialog` now returns whether anything changed, including in nested sub-item dialogs, and the board then refreshes the scope and hierarchy. `moveCard`/`reparentCard`/`rescheduleCard` update the Hierarchy/Roadmap items too. The long-term shared per-item store is still open; the B-H3 gap (board saves without a version) belongs there.
- **Where:** `board_view.dart:112-134`, `board_view_model.dart:287-370`, `work_item_detail_view.dart:376-378`
- **Issue:**
  - Edits in the detail dialog (title, assignee, tags, dates, layer, priority) never reach `BoardViewModel`.
  - `moveCard`, `reparentCard` and `rescheduleCard` update `_swimlanes` but not `_hierarchyItems`, while `assign`/`setTags` update both.
  - Deleting a sub-item from a nested dialog refreshes nothing.
- **Fix:** Short term: have the dialog return a "changed" result and refresh the scope and hierarchy. Long term: a shared per-item store that both features read from and write through.

### Medium

- [x] *(Resolved in `bugfix/token-refresh-stampede`.)* **F-M1. Near-expiry tokens and blanket 401 handling.** `isAccessTokenExpired` has no leeway, so a token with 1 s left expires in flight. Any 401 then clears the session without trying a refresh, even when a newer session already exists. **Fix:** treat the token as expired 30–60 s early, and only clear if the token that was sent is still the current one. (`auth_session.dart:21`, `auth_http_client.dart:31-33`)
- [x] *(Resolved in `bugfix/detail-dialog-fixes`.)* **F-M2. `unawaited(_repository.logout(...))` has no error handler**, so a network failure becomes an unhandled async error. (`auth_view_model.dart:58`)
- [x] *(Resolved in `feature/shared-api-client`:
  - Failures are typed: `ApiException` with a status code, `NetworkException` and `UnexpectedResponseException`.
  - View models catch only `ApiException`, so programming errors surface.
  - A token refresh clears the session only on a 401.)*

  **F-M3. `catch (_)` everywhere** (about 12 sites) swallows `TypeError`/`StateError` from JSON casts and `!`. A backend contract change looks like "check your connection", and in `auth_session_store.dart:70` any parsing bug logs the user out. **Fix:** catch `ApiException` and network exceptions only, and wrap parse failures in a typed exception.
- [x] *(Resolved in `feature/shared-api-client`: `core/network/json_api_client.dart` handles all HTTP/JSON, and the models have `fromJson` factories.)* **F-M4. HTTP/JSON plumbing is copied across three repositories.**
  - `_uri`, `_checkOk`, `_problemDetail`, `_getJsonList` and `_parseDate` are defined in `api_board_repository`, `api_work_item_detail_repository` and `api_auth_repository`.
  - Status, user and tag parsing is duplicated.
  - The copies are already drifting: one uses `queryParameters`, the other concatenates `?parentId=$id`.
  - **Fix:** a shared `core/network/json_api_client.dart` plus `fromJson` factories on the models.
- [x] *(Resolved in `feature/untangle-features`:
  - Shared models (`WorkItemStatus`, `User`) live in `lib/shared`, and `formatDate` is in `lib/core/dates`.
  - Cached `StatusRepository`/`UserDirectoryRepository` serve both features.
  - The board gets a `WorkItemDetailOpener` from the composition root (`app.dart`), and the detail view model gets a `CurrentUser`.
  - No feature imports another.)*

  **F-M5. Features are tangled.**
  - `board` imports the `work_item_detail` view.
  - `work_item_detail` imports `board/models/board_status.dart` and `board/widgets/date_format.dart`.
  - `AuthUser` doubles as the general user-directory model.
  - Both features re-fetch `/statuses` and `/users` independently.
  - **Fix:** move the shared models and formatting into `lib/core` or `lib/shared`, add cached `StatusRepository`/`UserDirectoryRepository`, and open the dialog through a callback or router.
- [ ] **F-M6. `BoardViewModel` is a god class** (733 lines, about 6 responsibilities: navigation, optimistic mutations, filters, sorting with two identical comparator switches, tree building, lookups). **Proposed split:** `BoardFilters` (immutable value plus predicates), `HierarchyTreeBuilder` (pure), a scope/navigation VM, a mutations helper, and a `UserDirectory` map.
- [ ] **F-M7. Every notify rebuilds the whole board.**
  - A single `ListenableBuilder` wraps the header and the `TabBarView`.
  - `hierarchyRoots`, `visibleStatuses` and `availableTags` are recomputed getters.
  - Per-card lookups are linear scans (O(cards × users)).
  - Search has no debounce.
  - **Fix:** memoise derived values, build id→entity maps once, narrow listeners, and debounce search by about 200 ms.
- [ ] **F-M8. Swimlane layout does repeated work and can overflow.**
  - `_rowHeightFor` is called twice per lane and creates a new `TextPainter` each time; no painter is ever disposed.
  - `IntrinsicHeight` wraps every card.
  - `GridRowBox` has a fixed height, but a card with a long title can grow past it and paint into the next lane.
  - (`swimlane_view.dart:136-306`, `status_column.dart`, `tag_badge.dart`)
- [x] *(Resolved in `feature/single-request-board-load`: `GET /api/work-items/swimlanes` returns every lane with its cards using two queries. With statuses cached, a board load is one request. Verified on the dev database: same lanes, cards and order as the per-lane requests, 1 request instead of 8.)* **F-M9. N+1 board load.** The client fetches `/statuses`, then children, *sequentially*, then one request per swimlane: 32 requests for a 30-lane board. This fan-out is also what triggers F-H1. **Fix:** add a backend endpoint that returns two levels at once, and at minimum run the first two requests in parallel.
- [ ] **F-M10. `WorkItemDetailView` is 608 lines.**
  - Seven `_buildXxx` methods should be widget classes.
  - The widget calls `getIt<AuthSessionStore>()` directly, bypassing the VM.
  - Save semantics are mixed: some fields need **Save** and others save immediately, and unsaved title edits are lost on close.
  - It accepts an empty title and start > end.
- [x] *(Resolved in `bugfix/detail-dialog-fixes`: tiles are keyed by comment id, and the tile re-seeds its editor on every edit and cancels it if its comment changes. Covered by a widget test.)* **F-M11. Comment tiles have no `key`.** `CommentTile` is stateful and seeds its edit controller once. After a deletion, a tile's State serves the next comment, so **Edit → Save can overwrite a different comment with the deleted comment's text**. **Fix:** `key: ValueKey(comment.id)`, and reset the controller when editing starts. ([work_item_detail_view.dart:470-477](frontend/lib/features/work_item_detail/work_item_detail_view.dart#L470-L477))
- [x] *(Resolved in `bugfix/detail-dialog-fixes`: `load` uses record `.wait`, `isSaving` counts overlapping saves, and comment/link mutations apply the server response instead of reloading.)* **F-M12. `WorkItemDetailViewModel` issues.**
  - `Future.wait` over a `List<Object>` with positional `as` casts. Use Dart 3 record `.wait`.
  - `_isSaving` is a single bool shared by concurrent operations.
  - "POST succeeded but the reload failed" is reported as a failure, so the user retries and creates a duplicate comment or link.
- [x] *(Resolved in `bugfix/time-filter-and-roadmap-dates`: bounds compare by calendar day, inverted ranges are prevented in the picker and swapped otherwise, and each bound has its own clear button.)* **F-M13. Time filter.** The end bound is effectively exclusive (items later on the "to" day are dropped), start > end is accepted, and a single bound can't be cleared on its own.
- [x] *(Resolved in `bugfix/time-filter-and-roadmap-dates`: `core/dates/calendar_days.dart` (`addDays`, `daysBetween`). The bar maths is extracted into a tested `roadmapBarSpan`, and the suite passes under Stockholm, New York and UTC.)* **F-M14. Roadmap date maths breaks across DST** (`add(Duration(days: n))` on local times, `inDays` truncation). **Fix:** use `DateTime(y, m, d + n)` and compare date parts only.
- [x] *(Resolved in `bugfix/hierarchy-layout`: when the columns are wider than the screen, the header and rows scroll horizontally together. Covered by a 400 px widget test.)* **F-M15. The Hierarchy view overflows narrow screens.** Its fixed columns total about 950 px in a plain `Row` with no horizontal scroll.
- [x] *(Resolved in `bugfix/hierarchy-layout`: `HierarchyColumn` covers every column, and widths live in an immutable `HierarchyColumnWidths` keyed by it. Layout constants moved to `hierarchy_layout.dart`, so the child widgets no longer import their parent view.)* **F-M16. Hierarchy column widths are keyed by magic strings with `!` lookups** (`columnWidths['number']!`). **Fix:** use a `Map<HierarchyColumn, double>`.

### Low

- [ ] **F-L1.** The `heirarchy` misspelling in the folder and 4 file names sits next to the correctly spelled `hierarchy_view.dart`. Fix with a dedicated `git mv` commit.
- [ ] **F-L2. One-class-per-file violations:** `tag_badge.dart`, `hierarchy_item.dart`, `create_work_item_dialog.dart` (+ result class), `assign_dialog.dart`, `secure_token_store.dart`, `auth_user.dart`.
- [ ] **F-L3. Duplicated view structure.** `roadmap_row.dart` and `heirarchy_row.dart` are identical, and `flatten` is duplicated. Child widgets import their parent views just for constants, which creates circular imports. **Fix:** a shared `FlattenedTreeRow` + `flattenTree()` in `models/`, and per-view `*_layout.dart` constants.
- [ ] **F-L4. Stale or "what" comments** (CLAUDE.md §2.5).
  - References to types that no longer exist (`[SwimlaneBoard]`, `_StatusColumn`, `_SwimlaneLabel`).
  - Contradictory layout notes.
  - Feedback history that belongs in commit messages (`assignee_avatar.dart:20-23`).
- [ ] **F-L5. `ApiConfig` fails late and breaks on a trailing slash.** A missing native `API_BASE_URL` throws only inside a widget initialiser, and `http://host/` becomes `//api`. Validate in `main()` and normalise the URL.
- [ ] **F-L6. `parseStatusColor`** renders the colour fully transparent if the `#` is missing, and throws on invalid input, which fails the whole board load. It has no tests.
- [ ] **F-L7. Unknown wire values fall back silently** (priority → medium, user kind → human). `WorkItemPriority.toWire()` uses the display label, which couples the UI text to the API contract.
- [ ] **F-L8.** `hiddenStatusIds` and `selectedTagFilters` expose mutable internal sets. Return `UnmodifiableSetView`.
- [x] *(Resolved in `bugfix/board-retry-replays-create`.)* **F-L9.** The FAB and create dialog stay available while the board is loading or has failed. `createWorkItem` reloads whatever scope is current when it *completes*, not the scope the item was created in.
- [ ] **F-L10. `pubspec_overrides.yaml`** pins `flutter_secure_storage` to 10.3.4 while `pubspec.yaml` declares `^11.2.0`. It was committed without explanation in `9cf3167`. Pin the version in `pubspec.yaml` with a reason, and delete the override.
- [ ] *(Formatting is now enforced in CI (`feature/ci-hardening`); the rest is still open.)* **F-L11. `analysis_options.yaml`** excludes `pubspec.yaml`, so `sort_pub_dependencies` never runs (and the dependencies are unsorted). Several rule comments are wrong. `flutter_lints` is one major version behind. Formatting isn't enforced in CI.
- [ ] **F-L12.** The `ViewModel` base class has no `isDisposed` for long operations to check, and there is no test for `notifyIfActive` after dispose.
- [x] *(Resolved in `bugfix/tag-filter-casing`.)* **F-L13. The tag filter is case-sensitive across items** *(found 2026-09-26 during the docs restructure)*. `availableTags` merges spellings case-insensitively ("Urgent" and "urgent" show as one chip), but `BoardViewModel.matchesTagFilter` uses exact `Set.contains`, so picking the chip hides items that use the other casing. Normalise both sides (e.g. compare lower-cased) and add a test.

---

## 3. Infrastructure, deployment and CI

### High

#### I-H1. Production compose publishes the API directly over plain HTTP, and `/mcp` isn't proxied
- [x] **Resolved** in `bugfix/deploy-exposure-and-tls`:
  - The backend publishes no host port.
  - Caddy (internal CA) terminates TLS on 80/443 and redirects HTTP.
  - nginx proxies `/mcp` unbuffered.
  - The API honours forwarded headers and no longer runs `UseHttpsRedirection`.
  - Verified with the production images: login, `/api` and an MCP handshake work over HTTPS, and the backend port is unreachable.
- **Where:** [compose.yaml:39-40, 47-48](compose.yaml#L39-L48), [nginx.conf:16-21](docker/frontend/nginx.conf#L16-L21)
- **Issue:**
  - The backend publishes `8080:8080` on the host, bypassing nginx.
  - Nginx serves `:80` with no TLS, so passwords and bearer tokens cross the network in clear text unless someone adds an external proxy that the docs don't mention.
  - Nginx proxies only `/api/`, so MCP clients *must* use the directly exposed `:8080/mcp`.
- **Fix:**
  - Stop publishing the backend port in `compose.yaml` (keep it in the override for dev).
  - Add `location /mcp` to nginx.
  - Document or add TLS termination (e.g. a Caddy or Traefik service).
  - Enable forwarded headers in the API (B-M8).

### Medium

- [x] *(Resolved in `feature/health-checks`: `/health` with a DB check; compose healthchecks on the backend and frontend; db → backend → frontend → caddy each wait for `service_healthy`.)* **I-M1. No health-based startup ordering.** `frontend` depends on `backend` without a health condition, and the backend has no healthcheck (B-M8).
- [x] *(Resolved in `feature/ci-hardening`: format check, `flutter analyze`, coverage artifacts for both stacks, image builds on PRs without pushing, and Flutter pinned to 3.44.0 in CI and both Dockerfiles. The frontend was reformatted in its own commit.)* **I-M2. CI gaps.**
  - The workflow doesn't run `flutter analyze` or `dart format --set-exit-if-changed`, doesn't build the Docker images on PRs (a broken Dockerfile is only found after merging to master), and doesn't pin the Flutter version (`channel: stable` floats).
  - The build image `ghcr.io/cirruslabs/flutter:stable` also floats, so CI tests and the shipped build can use different SDKs.
  - No coverage is reported even though `coverlet` is referenced.
  - **Fix:** add the analyze/format steps, run `docker build` without push on PRs, and pin one Flutter version for both CI and the Dockerfile.
- [x] *(Resolved in `bugfix/deploy-exposure-and-tls`: `.env.example` values are empty, so an unedited copy fails compose validation, and the API refuses signing keys under 32 bytes.)* **I-M3. Placeholder values that pass validation.** `.env.example` uses `changeme` for `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY`, and `compose.yaml` only checks that they are non-empty. Copying the example unchanged produces a running deployment with an 8-byte, publicly known signing key. The B-H1 key-strength check would also catch this.
- [x] *(Resolved in `feature/database-backups`: a `backup` service (official postgres image) dumps nightly plus at startup, keeping 7 daily and 4 weekly dumps. A restore was verified end to end.)* **I-M4. No backups.** The `pgdata` volume has no backup or restore procedure or container for a self-hosted deployment.

### Low

- [x] *(Resolved in `feature/rename-claude-md`.)* **I-L1.** The root rules file is committed as `claude.md` (lower-case). That works on Windows and macOS but not on case-sensitive tooling that looks for `CLAUDE.md`.
- [x] *(Resolved in `feature/docs-restructure`: new top-level `README.md` and `CHANGELOG.md`, plus `docs/architecture.md`, `docs/development.md` and `docs/open-questions.md`. `project_design.md` is trimmed to the design rules, and `agent_notes.md` is retired into these files.)* **I-L2.** `agent_notes.md` (50 KB) and `project_design.md` mix durable architecture decisions with milestone history and "open decisions" that have since been settled. Consider a lean `docs/architecture.md` (or ADRs) plus a changelog, and a top-level `README.md`; only `frontend/README.md` exists today.
- [x] *(Resolved in `bugfix/deploy-exposure-and-tls`.)* **I-L3.** `compose.yaml` defaults to `ghcr.io/OWNER/...`, so a bare `docker compose pull` fails with an unclear error. Make it a required variable (`${BACKEND_IMAGE:?…}`) like the others.
- [x] *(Resolved in `feature/nginx-hardening`: `nginx-unprivileged` (uid 101), CSP and other security headers, CanvasKit served locally, and `no-cache` revalidation for app files. Flutter's web output isn't content-hashed, so no long-lived caching is safe. Verified with a UI login in headless Chromium.)* **I-L4.** The nginx runtime image runs as root and sets no security headers (CSP, `X-Content-Type-Options`), and there is no cache policy distinguishing the hashed Flutter assets from `index.html`.

---

## 4. Testing

#### T-1. No integration tests against a real pipeline or database
- [ ] **Resolved**
- **Issue:** Backend tests are controller tests with mocked services, service tests on **EF InMemory**, and one `RankCalculator` suite. So the following are never tested:
  - the auth fallback policy, JWT validation, CORS and `ApiExceptionMiddleware` (no tests at all)
  - JSON enum serialization end-to-end
  - Postgres-specific behaviour: FK `Restrict` (B-H2), `xmin` concurrency (B-H3), `text[]` tags, identity `Number`, unique-index violations
  - MCP over HTTP with auth
- **Fix:** Add a `Weaver.Api.IntegrationTests` project using `WebApplicationFactory<Program>` + Testcontainers PostgreSQL. A handful of tests covering login → authorized call, 401 without a token, delete-with-links, and a concurrent-update 409 would cover the riskiest gaps.

#### T-2. No concurrency tests on the client
- [ ] **Resolved**
There are no `Completer`-based tests, so none of these are covered: parallel refresh (F-H1), overlapping mutations and rollbacks (F-H3), racing scope loads (F-H4), or concurrent detail saves (F-M12). There is also no test of `retry()` after a *successful* action (F-H2).

#### T-3. Almost no widget tests
- [ ] **Resolved**
`test/widget_test.dart` is a DI smoke test, and it is the only widget test. There is none for `BoardView`, `LoginView`, `AuthGate`, `WorkItemDetailView`, `CommentTile` (which would have caught F-M11), `CreateWorkItemDialog`, or drag/drop acceptance in `StatusColumn`/`SwimlaneLabel`.

#### T-4. Pure logic without unit tests
- [ ] **Resolved**
- **Frontend:** `ApiConfig.baseUrl`, `parseStatusColor`, `formatDate`, `RoadmapTimeframe`, the roadmap bar computation (private in a widget, so extract it first), and the hierarchy time-window filter.
- **Backend:** domain rules, once they move onto the entities (B-M2).

---

## 5. Suggested sequencing

Each step fits in one or two focused branches, in line with the CLAUDE.md git rules:

1. **`bugfix/token-refresh-stampede`** covers F-H1 and F-M1. Then **`bugfix/board-retry-replays-create`** covers F-H2 and F-L9.
2. **`bugfix/jwt-key-and-deploy-exposure`** covers B-H1, I-H1 and I-M3.
3. **`feature/api-integration-tests`** covers T-1. This unblocks and verifies the next step.
4. **`bugfix/delete-linked-work-item`** covers B-H2. Then **`feature/client-optimistic-concurrency`** covers B-H3, with DTO version and If-Match on the frontend.
5. **`bugfix/schedule-date-timezone`** covers F-H5, F-M13 and F-M14. This needs a backend `DateOnly` migration and a frontend change together.
6. **`bugfix/board-state-races`** covers F-H3, F-H4, F-H6 and F-M11.
7. **`feature/request-validation`** covers B-M1, B-M4 and B-M5.
8. Refactors, in this order:
   - the shared API client and shared models (F-M4, F-M5)
   - splitting the view models and views (F-M6, F-M10)
   - moving invariants onto the domain entities (B-M2)
   - the Application layer (B-M3)
9. Batched chores: `chore/rename-hierarchy-folder`, `chore/one-class-per-file`, `chore/lint-and-format-ci`, and stale-comment cleanup.
