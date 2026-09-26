# Architecture

How Weaver is built and why. Each section records the decisions that
aren't obvious from the code alone, and the pitfalls behind them. The
design rules themselves are in [project_design.md](../project_design.md);
the history is in [CHANGELOG.md](../CHANGELOG.md).

## Overview and layers

- **Backend:** ASP.NET Core 9, three projects under `backend/src/`:
  - `Weaver.Domain`: entities, enums, domain exceptions, `RankCalculator`,
    `TextValidation`. No dependencies on EF Core or ASP.NET.
  - `Weaver.Infrastructure`: `WeaverDbContext`, EF configurations and
    migrations, JWT token service, and the application services.
  - `Weaver.Api`: controllers, DTOs (`Contracts/`), MCP tools (`Mcp/`),
    `ApiExceptionMiddleware`, `Program.cs`.
- **Frontend:** Flutter (`frontend/lib/`), organised by feature (`auth`,
  `board`, `work_item_detail`) plus shared `core/` (DI, networking,
  `ViewModel` base, theme).
- **Database:** PostgreSQL 16 through EF Core (Npgsql). Migrations are
  applied at API startup (`Database.MigrateAsync()` in `Program.cs`).
- **Deployment:** Docker Compose: `db`, `backend`, `frontend` (nginx),
  `caddy` (TLS), `backup`.

**The Application layer lives in `Weaver.Infrastructure/Services`.**
`project_design.md` describes a Domain / Application / Infrastructure / API
split, but there is no `Weaver.Application` project. The service
interfaces (`IWorkItemService`, `IBoardService`, …) and their
implementations sit in `Weaver.Infrastructure/Services` and use
`WeaverDbContext` directly. Treat that folder as the Application layer.
Moving it out is tracked as B-M3 in
[code_review_findings.md](../code_review_findings.md).

**Every controller depends only on a service interface**, never on
`WeaverDbContext`. This lets controller tests mock the service with Moq,
and it is what allows MCP tools to reuse the same business logic (see
[MCP](#mcp)).

## Domain model

### Work items and boards

- **A board is scoped by an ancestor item, not by depth.** A board's
  swimlanes are the direct children of `Board.ScopeItemId` (or the
  top-level items when it is null); its cards are each swimlane's direct
  children. Depth-from-root was rejected: hierarchy depth is unrestricted
  and branches can have different depths, so "level N" doesn't generalise.
  Scoping by a node means any card can become the scope of a future board
  with no schema change, and drilling into a card on the frontend is just
  loading a different scope.
- **`ChangeStatus` and `Reparent` are separate operations**
  (`ChangeStatusAsync` / `ReparentAsync`, `POST /work-items/{id}/status` /
  `/parent`), each touching only `StatusId` or `ParentId`. That a column
  drag can never reparent an item, and vice versa, is guaranteed by the
  method signatures, not by trusting callers to send one field to a shared
  update endpoint. The frontend mirrors this (see
  [Frontend](#frontend)).
- **Cycle detection walks up from the proposed parent**
  (`WouldCreateCycleAsync`), one query per ancestor with a visited set,
  looking for the item being moved. A recursive SQL query would be fewer
  round trips, but this works the same on Postgres and on the EF InMemory
  provider used by the service tests. The direction matters: walk up from
  the new parent, not down from the item.
- **Deleting an item with children requires `cascade: true`**, otherwise
  `WorkItemHasChildrenException` (409). There is no silent subtree loss. A
  cascade loads all descendants and removes them in one `SaveChanges`; EF
  topologically sorts tracked deletes against the `Restrict` parent FK.
- **Delete removes links and refuses to delete a board's scope.**
  - Both `WorkItemLink` FKs are `Restrict`, because EF rejects two
    `Cascade`/`SetNull` paths from one table to the same principal. So
    `DeleteAsync` removes every link touching the deleted subtree itself,
    in the same `SaveChanges`. Without that, Postgres rejects the delete
    and the client gets an unmapped 500.
  - If the item or any cascaded descendant is some board's
    `ScopeItemId`, the delete is rejected with
    `WorkItemIsBoardScopeException` (409). A board is a saved view other
    users rely on; silently deleting it or widening it to top level would
    be a surprising side effect of deleting a card.
  - The InMemory provider doesn't enforce FKs, so these paths were
    verified by hand against Postgres (see [Testing](#testing-strategy)).
- **Rank is a fractional `double` scoped to `(ParentId, StatusId)`**, not
  globally unique. Drag-and-drop can insert between two siblings without
  rewriting anyone else's rank (`RankCalculator`). There is no rebalancing
  job; ties from concurrent inserts at the same spot currently make
  `RankCalculator` throw (B-M5 in the code review).
- **`Number` is a Postgres identity column** (`GENERATED ALWAYS AS
  IDENTITY`, unique), set only by the database and used for display
  (`#N`). `Id` (a GUID) remains the key everywhere else. The InMemory
  provider doesn't generate it, so it has no unit test.

### Layers, priority and other fields

- **`WorkItemLayer` is a lookup table** (`Id`, `Name`, `Order`), seeded
  with Project / Goal / Task via `HasData`, the same pattern as `Status`.
  `WorkItem.LayerId` is nullable and deliberately **not validated against
  the parent/child tree**: the layer is a label, not a constraint, so the
  hierarchy stays fluid (a Task can parent a Project if someone wants
  that).
- **`Priority` is a fixed enum** (`WorkItemPriority`: `Low`, `Medium`,
  `High`, `Urgent`; default `Medium`), like `StatusCategory`. Only layers
  and statuses were asked to be configurable.
- **Fields that were requested but map onto existing ones:**
  - There is no `DueDate`: `EndDate` is the due date (the board's sort
    option labels it "Due date").
  - There is no stored `ProjectId`: "which project is this under" is the
    nearest ancestor whose layer is Project. Storing it would duplicate the
    hierarchy and drift on reparent. A project concept independent of the
    tree would be a separate feature.
  - The requested `Type` field is `LayerId`.
  - `CreatedAtUtc` / `UpdatedAtUtc` are the created/updated timestamps.
- **Statuses** are seeded (To Do, Doing, Done) with a per-row `Color`, so
  colours can be changed without a schema change.

### Tags

- **`Tags` is a `List<string>` mapped to a Postgres `text[]` column**, the
  first scalar-list field on `WorkItem`. No join table and no index: all
  tag search and filtering happens on the client over data it already has.
- **Tags are set as a whole** (`SetTagsAsync`, `POST
  /work-items/{id}/tags`); there is no add/remove-one endpoint. The client
  always sends the complete list.
- **Normalisation is server-side:** each tag is trimmed, blank tags are
  rejected (`InvalidWorkItemTagException`, 400), and duplicates are removed
  case-insensitively with the first casing kept. At most
  `WorkItem.MaxTags` (20, counted after de-duplication) tags of at most
  `WorkItem.TagMaxLength` (50) characters. `text[]` has no limit of its
  own, so without these there was no bound.

### Comments and links

- **A link is one symmetric row.** `WorkItemLink` stores
  `WorkItemId` / `LinkedWorkItemId`, but `ListForWorkItemAsync` matches
  either column and `WorkItemLinkDto.FromEntity(link,
  perspectiveWorkItemId)` reports whichever side isn't the requesting
  item, so a link created from A is listed from B with the same id.
  Creating a duplicate in either direction (`DuplicateWorkItemLinkException`,
  409) or a self-link (`SelfWorkItemLinkException`, 400) is rejected.
- Links are untyped "related to" associations, not URLs or attachments
  (see [open-questions.md](open-questions.md)).
- Comments cascade-delete with their work item; editing is restricted to
  the author (see [Authentication](#authentication)).

### Schedule dates

- **`StartDate` / `EndDate` are calendar dates** (`DateOnly`, Postgres
  `date`), sent as `yyyy-MM-dd`, each optional. They were originally
  `DateTimeOffset`, and the client sent the picked day as local midnight
  converted to UTC, so east of UTC the stored value was the previous day.
  A day on a schedule is not an instant; storing it as one invites
  time-zone bugs.
- **They're set by their own operation**, `RescheduleAsync` (`POST
  /work-items/{id}/schedule`), separate from status, parent and create.
  The only rule (start not after end, when both are set) is checked before
  the entity lookup, so a bad range is reported even for a nonexistent id
  instead of being masked by a 404.
- **A missing date is open-ended, not "unscheduled"** (see the time
  filter under [Frontend](#frontend)).

## API

- **Operation-oriented endpoints** for state changes: `POST
  /work-items/{id}/status`, `/parent`, `/schedule`, `/assignee`, `/tags`,
  and `PUT /work-items/{id}/details` for title, description, layer and
  priority together. Those four share an endpoint because none of them
  carries the cross-cutting risk that keeps status and parent apart.
- **DTOs only** (`Weaver.Api/Contracts`); EF entities are never
  serialised.
- **Enums serialise as their string name** (`"Medium"`, `"Human"`) via a
  global `JsonStringEnumConverter` in `Program.cs`. Without it the API
  sent ints, and the frontend's `as String` cast failed on a response that
  had actually succeeded, surfacing as a misleading "could not create
  account". MCP needs the same setting separately (see [MCP](#mcp)).
- **Errors map to status codes in `ApiExceptionMiddleware`** as
  `ProblemDetails` bodies: not found → 404; cycles, has-children,
  board-scope, duplicate link, username taken, version conflict → 409;
  validation → 400; bad credentials / refresh token → 401; comment
  author mismatch → 403. The list is hand-maintained and duplicated in
  `McpExceptionTranslation` (B-M4 in the code review).
- **Input validation lives in the services**, via `TextValidation` in
  `Weaver.Domain`, so REST and MCP share it. A broken rule throws
  `DomainValidationException` (400 in REST, a tool error in MCP). Use it
  for new field rules instead of adding exception types. Max lengths are
  constants on the entities (`WorkItem.TitleMaxLength`,
  `Comment.BodyMaxLength`, `Board.NameMaxLength`) that the EF
  configurations also use, so the validation limit and the column limit
  can't drift. Before this, oversized input reached the database and
  returned a 500.

### Optimistic concurrency

- **`WorkItem.Version` maps to Postgres's `xmin`** as an EF concurrency
  token and is returned as `Version` on every `WorkItemDto`.
- On its own the token only guards the moment between a request's read and
  its write. So the operations that overwrite a group of fields (details,
  schedule, tags) also accept an optional `ExpectedVersion` and reject a
  stale one with `WorkItemVersionConflictException` (409). A
  `DbUpdateConcurrencyException` during save is translated to the same
  exception.
- **Status, parent and assignee stay last-write-wins on purpose.** Each is
  a single intentional change, and `xmin` moves on *any* update, so a
  version check there would reject a card drag because someone else edited
  the description.
- `ExpectedVersion` is optional so MCP clients that don't track versions
  keep working.
- The detail dialog sends it; on a conflict it reloads the item and
  re-seeds its form fields so the next save can't overwrite the other
  change with stale values. The board's own tag and reschedule calls don't
  send a version yet, because board cards don't track one.

## Authentication

- **Tokens:** a JWT access token (15 minutes,
  `JwtOptions.AccessTokenLifetime`) and an opaque refresh token (30 days,
  `JwtOptions.RefreshTokenLifetime`). Only the refresh token's SHA-256
  hash is stored (`RefreshToken.TokenHash`), so a database read alone never
  yields a usable credential.
- **Signing key:** `Jwt:SigningKey` is required; the API refuses to start
  without one or with one under 32 bytes (HS256's minimum), which would
  otherwise only fail at the first login. Note that
  `backend/src/Weaver.Api/appsettings.json` still ships a dev key, so the
  "missing key" check only fires if that file is overridden (B-H1, see
  [open-questions.md](open-questions.md)).
- **Passwords** are hashed with `PasswordHasher<User>` (PBKDF2-HMAC-SHA256,
  framework-managed iterations) from the standalone
  `Microsoft.Extensions.Identity.Core` package. The full ASP.NET Core
  Identity system (roles, claims, its EF schema) would impose far more
  shape than a username/password table needs. Passwords are 12–200
  characters; usernames 3–50 of letters, digits, `.`, `_`, `-`, compared
  case-insensitively.
- **Login failures are uniform.** Unknown username, wrong password and a
  locked-out account all throw `InvalidCredentialsException` with the same
  message, so the response can't be used to enumerate usernames or learn
  lockout state. Five failures lock the account for 15 minutes.
- **Every endpoint requires authentication by default**, through
  `AddAuthorization(options => options.FallbackPolicy = ...
  RequireAuthenticatedUser())`. A new controller is protected
  automatically. `AuthController` opts out per action with
  `[AllowAnonymous]` (register, login, refresh, logout); `/health` opts out
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
  of them. In production the app is same-origin through Caddy, so this
  only matters for another web origin calling the API
  (`CORS_ALLOWED_ORIGIN`, defaulting to `https://$WEAVER_DOMAIN`).
- **Comment edit/delete authorisation is in `CommentService`**, not the
  controller: `UpdateAsync` / `DeleteAsync` take the caller's user id (from
  the token via `User.GetUserId()`) and throw
  `CommentAuthorMismatchException` (403) for anyone but the author.

## MCP

- **Tools live in `Weaver.Api/Mcp/`**, not a separate project, because
  they return the same `Weaver.Api.Contracts` DTOs the controllers use.
  Each tool class mirrors one controller (`WorkItemTools` ↔
  `WorkItemsController`, etc.) and takes the same service interface. The
  SDK (`ModelContextProtocol.AspNetCore`) resolves a tool instance per call
  from the request's DI scope, so a `WeaverDbContext`-backed service
  behaves exactly as it does for a controller. `CommentTools` reads the
  caller's id through `IHttpContextAccessor`.
- **Coverage:** work items, boards, statuses, layers, users, comments and
  links. Auth endpoints are not tools; they're how a client gets its token.
  There is no "list all work items" tool; `list_work_item_children` with
  `parentId` omitted returns top-level items.
- **Auth is the REST API's.** `app.MapMcp("/mcp")` sits in the same
  pipeline after `UseAuthentication()` / `UseAuthorization()`, so it
  inherits the fallback policy. A request with no bearer token gets a plain
  401 before reaching MCP handling.
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
  production it's reached through Caddy → nginx; nginx proxies `/mcp`
  unbuffered with a one-hour read timeout because responses can be
  server-sent event streams.

## Frontend

### Structure and DI

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

- **Repositories behind interfaces** (`BoardRepository`,
  `WorkItemDetailRepository`, `AuthRepository`), each with an `Api…`
  implementation. View models depend only on the interface; tests use
  small stubs. The detail repository is separate from the board's because
  its concerns (full record, layers, users, per-field saves) differ from
  swimlane assembly and drag-and-drop.
- **Base URL:** `ApiConfig.baseUrl` always ends in `/api`. On web it
  defaults to the relative `/api`, so the same built image works at any
  domain (nginx proxies it). Native builds and the web dev server pass the
  backend origin with `--dart-define=API_BASE_URL=http://host:port` and
  the app appends `/api`. Getting this wrong makes every request 404 with
  nothing but "Failed to load resource" in the console. Repositories take
  the resolved URL as `@Named('apiBaseUrl')` instead of calling
  `ApiConfig` (which throws on native without the define), so they're
  testable with `http`'s `MockClient`.
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

- **`BoardViewModel.moveCard(card, newStatusId)` takes no lane
  parameter**; it uses the card's own `parentId`, so a status move can't
  cross lanes. The UI adds a second layer: the `StatusColumn` drag target
  only accepts cards whose `parentId` matches its lane. Reparenting uses a
  separate drag target on `SwimlaneLabel` that only accepts cards from
  *other* lanes. At model level, `WorkItemCard.copyWith` never touches
  `parentId`; `movedToParent` is the separate reparent path.
- **Scope changes go through `_changeScope`.** `load`, `drillInto`,
  `navigateToBreadcrumb` and `refreshCurrentScope` all use it: it sets the
  loading flag, catches failures into `loadError`, and records a `_retry`
  closure **only when the action fails**.
  - `retry()` re-attempts the failed navigation and is a no-op otherwise.
    Use `refreshCurrentScope()` to reload what's shown. `retry()` used to
    double as refresh and replayed whatever ran last, pushing a breadcrumb
    again or re-creating an item.
  - `createWorkItem` deliberately doesn't use `_changeScope`: a failed
    create reports through `moveError` and keeps the board, and a create
    must never become the retry target.
  - `loadRootScopeItemId()` resolves the initial scope once, in `load()`;
    later navigation reuses ids already known client-side, and a
    breadcrumb's title is just the card title captured at drill-in.
- **Generations guard against overlapping loads.** Every scope load bumps
  `_scopeGeneration` and every hierarchy load bumps
  `_hierarchyGeneration`. A load's result, and the clearing of its
  spinner, is only applied if no newer load started meanwhile, so a slow
  older response can't overwrite a newer scope.
- **Optimistic mutations go through `_optimistic`** (apply, persist,
  revert lanes, revert hierarchy). The revert undoes only that one item's
  change by id, and is skipped if the lanes or hierarchy were reloaded
  since. Restoring a whole-list snapshot, as before, could undo a
  different change that had succeeded in the meantime. Moves, reparents,
  reschedules, assigns and tag changes update both the swimlane cards and
  the Hierarchy/Roadmap items.
- **The detail dialog reports whether it changed anything.**
  `showWorkItemDetailDialog` returns `true` if a field, tag, schedule or
  assignee was saved, the item was deleted, or a nested sub-item dialog
  reported a change. The board then refreshes the current scope and, if
  loaded, the hierarchy. Detail edits go through
  `WorkItemDetailRepository`, not `BoardViewModel`, so without this the
  board showed stale data. After "View sub-items" only the hierarchy is
  refreshed: the drill-in already loads the new scope, and refreshing the
  old one would be the newer load and cancel it. The longer-term fix is a
  shared per-item store both features read from.

### Board, filters and views

- **Search, status filter, tag filter, sort and the time-frame filter are
  client-side.** The board already fetches each lane's full child list,
  and the Hierarchy/Roadmap views fetch `GET /work-items/all`, so these are
  predicates and comparators over data in hand. None of them is persisted.
- **Time-frame filter semantics are interval overlap, not containment**,
  with a missing bound (the item's or the filter's) open-ended. An item
  with only a start date matches any frame ending at or after it; one with
  only an end date matches any frame starting at or before it.
  `BoardViewModel.matchesTimeFilter` implements this; its tests cover the
  open-ended cases, where overlap and containment diverge. Both bounds are
  inclusive whole days, each can be cleared on its own, and an inverted
  range can't be picked (and is swapped if set programmatically). It is not a
  sprint system: there is no fixed-length cycle, and the frame isn't
  saved.
- **`CardSortOption.manual` has a `null` comparator** and `StatusColumn`
  skips sorting entirely in that case. Dart's `List.sort` isn't stable, so
  an always-0 comparator could reshuffle the rank order. Sorting never
  touches the backend `Rank`.
- **Tapping a card opens its detail dialog**; drilling in is the dialog's
  "View sub-items" action. Any item can be drilled into, even without
  children: there's no `hasChildren` flag, and computing one only to grey
  out leaves would cost an extra fetch or a denormalised count.
- **Drag and drop uses Flutter's `Draggable` / `DragTarget`**, no package.
  There's no reordering within a column yet.
- **Roadmap is a hand-rolled Gantt widget** reusing
  `BoardViewModel.hierarchyRoots`, so it gets the same search, filters and
  collapse tree as Hierarchy. Its timeframe (zoom) and visible window are
  separate view state and deliberately not tied to the header's time-frame
  filter, which decides which items appear at all.
- **Hierarchy columns:** the caret and number column stay at a fixed x
  position and only the title indents, so fixed-width, resizable columns
  stay aligned with their headers at every depth.
- **Assignee avatars** are tappable everywhere they're shown.
  `AssigneeAvatar(showPlaceholderWhenUnassigned: true)` shows a silhouette
  on swimlane labels, Hierarchy rows and the assign dialog so every row has
  something to tap; board cards omit it to stay compact.
- **`debugShowCheckedModeBanner: false`**: the debug banner sat over the
  top-right app-bar actions and swallowed clicks on sign-out.

## Deployment and infrastructure

### Compose file split

- **`compose.yaml` is the deployment definition.** It only references
  pre-built images (`BACKEND_IMAGE` / `FRONTEND_IMAGE` at
  `WEAVER_IMAGE_TAG`), with no `build:` and no source mounts. It is what
  the server runs with `docker compose pull && docker compose up -d`.
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

- **Caddy with its internal CA terminates TLS.** The server is LAN-only
  with no public domain, so Let's Encrypt isn't an option. `caddy` runs
  `caddy reverse-proxy --from https://$WEAVER_DOMAIN --to frontend:8080
  --internal-certs`, owns host ports 80/443 and redirects HTTP to HTTPS.
  It's configured on the command line so the server still needs only
  `compose.yaml` and `.env`. The CA keys live in the `caddy-data` volume;
  each device trusts the root once (see the README).
- **The backend publishes no port in production.** Everything, including
  `/mcp`, goes Caddy → nginx → backend, so nothing reaches the API without
  TLS. nginx passes Caddy's `X-Forwarded-Proto` through (falling back to
  its own scheme), and the API trusts forwarded headers from up to two
  proxies (`UseForwardedHeaders`, `ForwardLimit = 2`, known networks and
  proxies cleared because Compose IPs aren't fixed). There is no
  `UseHttpsRedirection`: TLS ends at Caddy, and in-container redirection
  only logged a warning.
- **The app calls the API same-origin.** nginx proxies `/api/` and `/mcp`
  to `backend:8080`, so no API URL is baked into the frontend image; the
  domain is only known at deploy time.
- **Both frontend images listen on 8080** so prod and dev share one mental
  model; only the host side differs (Caddy → 8080 in prod, `8082:8080` in
  dev).

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
- Unknown paths fall back to `index.html` for client-side routing.

### Backend image

- Multi-stage: publish on the .NET SDK, run on the Alpine ASP.NET runtime
  as the non-root `$APP_UID`.
- **Invariant globalisation** (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`)
  keeps the image small; it's a JSON API with no locale-aware formatting
  (that's the client's job). Revisit if the backend ever formats for
  locales itself, e.g. exports.

### Startup, health and backups

- **Startup is ordered by health.** The API exposes an anonymous `/health`
  with a database check, not proxied by nginx. Compose waits db → backend
  → frontend → caddy on `service_healthy`, so Caddy never serves a
  frontend whose API is still migrating. The backend healthcheck has a
  30 s `start_period` for migrations. The dev override relaxes the
  frontend's wait to `service_started`, because the first `dotnet watch`
  build can outlast the healthcheck.
- **Backups are an inline `backup` service on `postgres:16-alpine`**,
  which already has `pg_dump`. A third-party backup image would need the
  database password, and inline configuration keeps the server at
  `compose.yaml` + `.env`. Schedule, retention and restore are in the
  README.

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
  emulator or device a generic Linux container can't provide; they build
  natively and can point at the dockerised backend
  (`http://localhost:8080`, or `http://10.0.2.2:8080` from the Android
  emulator).

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
  `xmin` or identity columns, and has no `text[]` or unique-index
  behaviour. So link/board-scope deletes, `WorkItem.Version` conflicts, the
  refresh-token race and `Number` generation were verified manually against
  real Postgres, not by automated tests.
- **Integration tests are still missing** (T-1 in the code review): no
  `WebApplicationFactory` + Testcontainers suite covering the auth
  pipeline, JSON enum serialisation, Postgres-specific behaviour or MCP
  over HTTP.
- **Frontend:** view-model and repository unit tests with stub
  repositories and `MockClient`, including `Completer`-based concurrency
  tests for the shared refresh and for the board's overlapping loads and
  rollbacks; few widget tests.
- **Headless-browser checks** of the running web app need special
  handling because Flutter renders to a canvas; see
  [development.md](development.md#verifying-flutter-web-in-a-headless-browser).
