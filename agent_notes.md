# Agent notes

Design decisions made while building this project that aren't obvious from
reading the code/config alone, and the reasoning behind them. Aimed at
whoever (human or agent) next touches this area.

## Domain model (backend)

- **Board scope, not depth number.** A board's swimlanes are the direct
  children of `Board.ScopeItemId` (or top-level items when null); cards are
  each swimlane's direct children. Depth-from-root was considered and
  rejected — hierarchy depth is unrestricted and branches can be
  inconsistent depths, so "level N" doesn't generalize. Scoping by a
  specific ancestor node does, and it means a card today can become a
  future board's scope with zero schema change.
- **`ChangeStatus` and `Reparent` are separate service methods**, each
  touching only one of `StatusId`/`ParentId`. This is what actually
  guarantees a column drag can never reparent an item and vice versa — it's
  enforced by the method signatures, not by trusting callers to only send
  one field on a shared "update" endpoint.
- **Cycle detection walks the ancestor chain in-process** (one query per
  hop, capped by a visited-set) rather than a single recursive SQL query.
  Slightly more round trips, but works identically against Postgres and
  the EF Core in-memory provider, so `WorkItemService` unit tests don't
  need a real database. Direction matters here and I got it backwards on
  the first pass — the check walks *up* from the proposed new parent
  looking for the item being moved, not from the item down.
- **Deleting a work item with children requires an explicit `cascade`
  flag.** No silent subtree loss. Cascade delete loads the full descendant
  set and hands it to EF Core's change tracker in one `SaveChanges` call
  rather than deleting bottom-up by hand — EF already topologically sorts
  tracked deletes against Restrict-behavior FKs correctly.
- **Rank is a fractional double**, scoped to `(ParentId, StatusId)` — not
  globally unique. Lets drag-and-drop reordering insert between two
  siblings without rewriting anyone else's rank. No rebalancing job exists
  yet; add one if repeated inserts at the same spot ever exhaust double
  precision (extremely unlikely at realistic board sizes).
- **`WorkItem.Version` maps to Postgres's `xmin`** as an EF Core
  concurrency token, and is returned as `Version` on every `WorkItemDto`.
  On its own the token only guards the moment between a request's read
  and its write, so the operations that overwrite a group of fields
  (details, schedule, tags) also accept an optional `ExpectedVersion`
  and reject a stale one with a 409 (`WorkItemVersionConflictException`).
  Status, parent and assignee stay last-write-wins on purpose: each is a
  single intentional change, and `xmin` moves on *any* update, so a
  version check there would reject a card drag because someone else
  edited the description. `ExpectedVersion` is optional so MCP clients
  that don't track versions keep working. The detail dialog sends it and,
  on a conflict, reloads the item and re-seeds its form fields so the
  next save can't overwrite the other person's change with stale values.
  This is why `WorkItemService`
  tests use the EF Core in-memory provider rather than mocking
  `DbContext` — the in-memory provider still gives real
  create/read/update/delete semantics for the invariants under test; a
  mocked `DbContext` would just be re-asserting whatever the mock was
  told to return.
- **`WorkItemsController` depends only on `IWorkItemService`**, not
  `WeaverDbContext` — reads (`GetById`/`GetChildren`) moved into the
  service alongside the writes. This was originally split (reads hit the
  DbContext directly, writes went through the service); it got unified
  when adding Moq-based controller tests, since a controller mixing a
  mockable service with a concrete DbContext can't be fully isolated in a
  unit test. `BoardsController`/`StatusesController` were deliberately
  *not* given the same treatment — they're thin CRUD/list wrappers with no
  invariants worth isolating behind a mock yet; introducing
  `IBoardService`/`IStatusService` for them now would be abstraction
  without a current reason.

## Input validation

- **Field rules live in the services, not the controllers**, via
  `TextValidation` in `Weaver.Domain`, so REST and MCP share them. A
  broken rule throws `DomainValidationException`, which both
  `ApiExceptionMiddleware` and `McpExceptionTranslation` map to 400. Use
  it for any new field rule instead of adding a new exception type.
- **Max lengths are constants on the entities** (`WorkItem.TitleMaxLength`,
  `Comment.BodyMaxLength`, `Board.NameMaxLength`) and the EF
  configurations use the same constants, so the validation limit and the
  column limit can't drift apart. Before this, oversized input reached
  the database and came back as an unmapped `DbUpdateException` (500).

## Testing

- NUnit + Moq, per explicit preference (originally scaffolded with xUnit).
- Moq is used only where there's a real interface boundary worth isolating
  — `IWorkItemService` in the controller tests. It is *not* used to mock
  `WeaverDbContext` in the service tests; EF Core's in-memory provider is
  the standard, non-fragile way to test EF Core-backed logic, and mocking
  `DbContext`/`DbSet` directly is a well-known anti-pattern (you end up
  testing the mock's setup, not the query).

## Docker / Compose

- **Two-file Compose split**: `compose.yaml` is the deployment definition —
  it only ever references pre-built images (`BACKEND_IMAGE`/
  `FRONTEND_IMAGE`, defaulting to the GHCR images the CI workflow
  publishes) and has no `build:`, no source bind mounts. `compose.override.yaml`
  is picked up automatically by `docker compose up` (Compose's default
  merge behavior — no flag needed) and swaps in `build:` + bind mounts +
  dev commands for local development. This means the exact same
  `compose.yaml` a developer's `docker compose up` starts from is also
  what a self-hosted server runs via `docker compose pull && docker
  compose up -d` — the override is what changes the story from "run the
  pulled images" to "build and hot-reload from source," and it should
  never be copied to the deployment server.
- **Compose merges `environment:` per-key and `ports:` as a union, not a
  wholesale replace.** This bit me once already: the override didn't set
  `ASPNETCORE_ENVIRONMENT`, so the base file's `Production` value silently
  won even inside the dev container, overriding the dev `Dockerfile`'s own
  `ENV ASPNETCORE_ENVIRONMENT=Development` (compose-level `environment:`
  always beats an image-baked `ENV`). Fixed by setting it explicitly in
  the override. If you add a new env var to `compose.yaml`'s
  `backend`/`frontend` services later, check whether the override needs
  its own value for it too — it won't inherit "nothing," it inherits the
  base's value. (`ports:` unioning, by contrast, is harmless here: the
  frontend ends up with both `80:8080` and `8082:8080` mapped in dev, and
  both work since the container always listens on 8080.)
- **Frontend container listens on 8080 in both prod and dev images**
  (nginx's `listen` directive was changed from the default 80), purely so
  the two Dockerfiles don't need different mental models — only the
  *host*-side port differs between prod (`80:8080`) and dev (`8082:8080`,
  chosen to avoid needing root to bind <1024 and to dodge collisions with
  other local dev servers on 80/8080).
- **The Flutter app should call the backend at a same-origin relative
  path (`/api/...`) in the web build**, not an absolute URL baked in at
  image build time. `docker/frontend/nginx.conf` reverse-proxies `/api/`
  to the `backend` service on the compose network, so the same built
  image works regardless of what domain/IP the self-hosted server ends up
  reachable at — the domain is only known at deploy time, not at CI build
  time. No Dart networking code exists yet to consume this; when it's
  written, it should default to a relative base URL for web and require
  an explicit configured absolute URL for native (Android/iOS) builds,
  since "same origin" isn't a concept there. In dev, `flutter run` doesn't
  proxy anything, so `API_BASE_URL` is passed via `--dart-define` instead
  (see `docker/frontend/dev-entrypoint.sh`), pointing at the
  host-mapped backend port.

  Verified end to end: built both prod images (backend 185MB, frontend
  132MB), ran db + backend + frontend together via `compose.yaml` against
  local image tags, and confirmed `curl http://localhost/api/statuses`
  through the nginx proxy actually reaches the backend and returns the
  seeded statuses, and that an arbitrary client-side route still falls
  back to `index.html` instead of 404ing.
- **Backend prod image is Alpine + invariant globalization**
  (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`) to keep it small — this
  is a JSON API with no culture-aware formatting of its own today (that's
  a Flutter/client concern). Revisit if the backend ever needs to do
  locale-aware formatting server-side (e.g. an export/report feature).
- **Only web is containerized for frontend dev.** Android/iOS toolchains
  need an emulator or real hardware that a generic Linux container can't
  provide (KVM passthrough for an Android emulator in Docker is possible
  but heavy and out of scope here). Android/iOS development happens
  natively on the host; it can still point at the dockerized backend
  (`http://localhost:8080` from the host, `http://10.0.2.2:8080` from the
  standard Android emulator's alias for the host machine).
- **Flutter hot reload inside a container is not automatic by default** —
  `flutter run` only reloads on an `r`/`R` keypress; there's no built-in
  file-watcher for headless/non-IDE use (IDEs like VS Code/IntelliJ
  implement that themselves by watching files and calling the VM service
  directly). `docker/frontend/dev-entrypoint.sh` works around this with
  the standard trick: a named pipe as `flutter run`'s stdin, and an
  `inotifywait` loop that writes `r` to it whenever `lib/` or
  `pubspec.yaml` change. This is a well-known but somewhat fragile
  pattern. Verified it for real: built the dev image, ran it standalone
  with `frontend/` bind-mounted, confirmed `flutter run -d web-server`
  came up and served on 8080, then `touch`ed `lib/main.dart` from the
  host and watched the container logs print `Performing hot reload...`
  with no keypress — the inotify → fifo → `flutter run` stdin path works.
  If it ever misses a change, the container still has `stdin_open`/`tty`
  set, so `docker compose attach frontend` and pressing `r`/`R` by hand is
  the guaranteed-to-work fallback.
- **GHCR image ownership is a placeholder.** `compose.yaml` defaults
  `BACKEND_IMAGE`/`FRONTEND_IMAGE` to `ghcr.io/OWNER/weaver-backend` /
  `-frontend`; `.env.example` calls this out. The CI workflow itself
  doesn't have this problem — it resolves the owner dynamically via
  `github.repository_owner` — but the deployment-side compose file can't,
  since it isn't running inside GitHub Actions. Update `.env` (not
  `.env.example`, and not `compose.yaml`) once the repo has a home on
  GitHub.
- **`POSTGRES_PASSWORD` has no default in `compose.yaml`** (`${VAR:?...}`
  syntax) so a deployment missing it fails immediately instead of
  silently running with a well-known weak password. The dev override
  hardcodes `weaver` for both, which is fine since it's non-production and
  the compose merge for `environment:` overrides it per-key.

## CI

- `.github/workflows/docker-publish.yml` runs backend (`dotnet test`) and
  frontend (`flutter test`) test jobs on every push and PR to `master`;
  the two image-publish jobs each depend on their own component's test job
  and are additionally gated on `github.event_name != 'pull_request'`, so
  PRs get test feedback but never push images (this also avoids needing
  package-write permissions on PR runs from forks).
- Images are tagged `latest` (default branch only), by semver on a `v*`
  tag push, and by commit SHA on every push — via `docker/metadata-action`,
  not hand-rolled tag logic.
- **`github.repository_owner` is not safe to drop directly into an image
  reference** — it preserves the GitHub account's actual casing, but
  Docker image names must be lowercase. Both publish jobs lowercase it
  into a step output (`${OWNER,,}`) first. This class of bug is exactly
  why the image-owner placeholder in `compose.yaml`/`.env.example` is
  literally `OWNER` (uppercase) rather than something that looks like a
  real, already-lowercase value — it was deliberately made to fail loudly
  (invalid Docker reference) if someone forgets to replace it, rather than
  silently resolving to a wrong-but-valid-looking image name.

## Flutter shell (frontend)

- **`injectable` is pinned to `^2.7.1+4` / `injectable_generator` to
  `^2.9.1`, not the `3.x` line.** `injectable_generator` 3.x replaced
  `build_runner`/`source_gen` with `lean_builder`, which pulls in
  `source_span ^1.10.2`; the Flutter SDK's bundled `flutter_test` pins
  `source_span 1.10.1`, so `3.x` can't resolve alongside `flutter_test` at
  all (`flutter pub add` fails version solving outright). The `2.x` line
  still uses `build_runner`, which coexists fine. Revisit the pin once
  either the Flutter SDK's `flutter_test` bumps its `source_span`
  constraint or `injectable_generator` stops requiring `lean_builder`.
- **`ViewModel` (`lib/core/presentation/view_model.dart`) is a thin
  `ChangeNotifier` wrapper, not a full MVVM framework.** It exists only to
  add `notifyIfActive()` — a dispose guard, since a ViewModel's async work
  can complete after its View is torn down and `ChangeNotifier.dispose()`
  makes a subsequent `notifyListeners()` throw. Views resolve their
  ViewModel via `getIt<T>()` (registered `@injectable`, i.e.
  factory-scoped — a new instance per resolution) in `State.initState`-time
  field init, and are responsible for calling `dispose()` on it in their
  own `dispose()`. No `provider`/`riverpod` dependency was added for
  this — `ListenableBuilder` (built into the Flutter SDK) is enough to
  rebuild a View when its ViewModel calls `notifyListeners()`, and pulling
  in a state-management package wasn't justified for a shell with one
  screen.
- **`analysis_options.yaml`'s `always_use_package_imports` and
  `specify_nonobvious_property_types` rules are enforced, not
  aspirational** — `flutter analyze` fails style review on relative
  intra-`lib` imports and on inferred-but-non-obvious field/variable
  types (e.g. `final x = getIt<Foo>();`). Two unrelated warnings
  (`switch_on_type`, `unnecessary_unawaited` "not a recognized lint rule")
  show up on a clean `flutter analyze` run — pre-existing, from a
  `flutter_lints`/lint-rule-name version mismatch, not something this
  work introduced.
- Flutter web renders to a `<canvas>` (via `flt-glass-pane`), not to real
  DOM text nodes — verifying the shell with a headless browser means
  waiting and screenshotting, not `waitForSelector('text=...')` or
  reading `document.body.innerText` (both come back empty even on a
  fully-rendered page). Confirmed the shell renders — title, theme, and
  placeholder body copy all visible — via a Playwright screenshot against
  `flutter run -d web-server`.

## Swimlane board prototype (Milestone 4)

- **`BoardRepository` is an abstract interface with one implementation,
  `FakeBoardRepository`, holding hardcoded in-memory fixture data.** This
  isn't speculative — Milestone 5 ("API-connected board") is the very next
  planned milestone and will add a second implementation backed by the
  REST API. `BoardViewModel` depends only on the interface, so that swap
  won't touch the view or view model, the same way `WorkItemsController`
  on the backend depends on `IWorkItemService` rather than a concrete
  class.
- **`BoardViewModel.moveCard(card, newStatusId)` takes no swimlane
  parameter — it looks up the card's own lane via `card.parentId`.** This
  makes a cross-swimlane move structurally impossible from this method
  alone, mirroring the backend's `ChangeStatus`/`Reparent` split (see the
  Domain model notes above): the invariant is enforced by what the method
  signature lets you express, not by trusting the caller. The UI adds a
  second, independent layer of enforcement on top —
  `DragTarget.onWillAcceptWithDetails` in `board_view.dart` rejects a drop
  whose card's `parentId` doesn't match the target column's swimlane —
  so even a future caller of `moveCard` that got the lookup wrong
  couldn't move a card across lanes through the board UI.
- **Drag-and-drop is Flutter's built-in `Draggable`/`DragTarget`, not a
  package.** No reordering *within* a column (by rank) yet — that needs a
  `Rank` concept on the frontend, which doesn't exist until the API
  connects in Milestone 5 and starts returning real ranks. This
  milestone only proves the status-column move.
- **Verifying drag-and-drop with a headless browser needs raw
  `mouse.move`/`down`/`up` sequences, not Playwright's element-based
  `dragAndDrop` helper.** Flutter web has no draggable DOM elements to
  target (see the canvas-rendering note above) — confirmed both that a
  same-swimlane drag moves a card between status columns, and that a
  cross-swimlane drag attempt is silently rejected, via coordinate-based
  mouse simulation against `flutter run -d web-server` and before/after
  screenshots.

## API-connected board (Milestone 5)

- **No CORS existed on the backend before this milestone** — added an
  open (`AllowAnyOrigin`/`AllowAnyMethod`/`AllowAnyHeader`) policy in
  `Program.cs` rather than a configured allowlist, since there's no auth
  yet (Milestone 10) and therefore no credentialed request to protect.
  Revisit and narrow this once auth exists.
- **`ApiConfig.baseUrl` always ends in `/api`, regardless of which branch
  produced it.** Native builds and web-dev overrides both pass just the
  backend's origin via `--dart-define=API_BASE_URL=http://host:port` (see
  `docker/frontend/dev-entrypoint.sh`'s existing default) — the app appends
  `/api` itself, the same way nginx's `/api/` proxy prefix does in prod.
  Got this backwards on the first pass (returned the raw dart-define value
  unchanged for web), which made every request 404 against the dev
  container silently — no console error surfaced client-side beyond
  "Failed to load resource: 404", so this is worth checking first if the
  connected board loads with an empty board and no obvious cause.
- **`ApiBoardRepository` takes its base URL as a constructor parameter
  (`@Named('apiBaseUrl')`, provided by `NetworkModule`) instead of calling
  `ApiConfig.baseUrl` directly.** `ApiConfig.baseUrl` throws on native if
  `API_BASE_URL` isn't set, which would make the repository un-instantiable
  in a plain `flutter test` run (no `--dart-define`, and `kIsWeb` is false
  under the VM test runner). Injecting the resolved string keeps the
  repository trivially testable with `http`'s `MockClient` and no
  dart-define ceremony.
- **`BoardRepository.changeStatus` is a separate method from `loadBoard`**,
  not a generic "save board" call — `BoardViewModel.moveCard` applies the
  status change optimistically to local state first, then calls
  `changeStatus` and rolls the local state back if it throws. This keeps
  the same optimistic-UI shape regardless of which repository implementation
  is behind it.
- **`FakeBoardRepository` was deleted, not kept alongside the API-backed
  one.** Its own doc comment said it was the only implementation "until
  Milestone 5 swaps in one backed by the REST API" — once that swap
  happened nothing referenced it, including tests (the widget smoke test
  and the view-model unit tests each define their own small stub/test
  double instead, so they don't depend on network at all).
- **Verifying this in a browser needed one extra step beyond prior
  milestones: `flutter run -d web-server` (this Flutter/dwds version, at
  least) doesn't call `main()` until something completes a DWDS debug
  handshake** (an SSE connection normally established by the Dart Debug
  Chrome extension or an IDE). A plain headless-browser `nav` sits forever
  on the DDC module-loading step with no console error. Confirmed this by
  waiting 4 minutes with full console/network capture (all 546 requests
  succeeded, nothing pending) before finding `window.$dartRunMain()`
  defined but never invoked; calling it manually from the test script
  unblocks rendering immediately. This is a verification-tooling quirk,
  not applicable to a real browser tab (which most workflows point at
  Chrome/Edge devices or the Dart Debug extension specifically to avoid
  it) — but it's what any future headless verification of this app will
  hit, so the workaround is worth keeping.

## Hierarchy: drill-down + reparenting (Milestone 6)

- **No backend work was needed for this milestone.** `WorkItemsController`
  already had `Reparent` (cycle detection included) since Milestone 1/2,
  and the board's "scope item" concept (`Board.ScopeItemId`) was designed
  from the start to support exactly this — drilling into a card just means
  asking `GetChildrenAsync` for a different `parentId`. This milestone was
  entirely a frontend exercise in exposing hierarchy that already existed.
- **`BoardRepository.loadBoard` now takes an explicit `scopeItemId`
  parameter instead of resolving "the board's scope" internally.** A
  separate `loadRootScopeItemId()` resolves the *initial* scope (the first
  `Board`'s `ScopeItemId`, or top-level) once; `BoardViewModel` calls it
  only from `load()`, and reuses whatever card/breadcrumb id is already
  known client-side for every subsequent navigation. No extra network
  round trip is needed to know a breadcrumb's title — it's just the
  card's own `title`, captured at drill-in time.
- **`BoardViewModel` funnels every scope change (`load`, `drillInto`,
  `navigateToBreadcrumb`) through one private `_changeScope` helper** that
  sets `isLoading`, clears `loadError`, records a `_retry` closure, and
  catches failures into `loadError` with an action-specific message. This
  is what makes a single generic `retry()` correct regardless of which
  navigation failed — same shape as `moveCard`/`reparentCard` sharing an
  optimistic-apply-then-rollback pattern for the same reason (one place to
  get the error handling right, reused by every mutating/loading action).
- **`WorkItemCard.movedToParent` is a separate method from `copyWith`**,
  deliberately not a `copyWith(parentId: ...)` overload. `copyWith`'s own
  doc comment already promises it never touches `parentId` — reusing it
  for reparenting would either break that promise or need a separate
  method anyway, and the split mirrors the backend's `ChangeStatus`/
  `Reparent` separation at the model level, the same way `BoardViewModel`
  already mirrors it with two distinct optimistic-update methods.
- **Reparenting via drag-and-drop needed a second, separate `DragTarget`**
  (on the swimlane's label, in `_SwimlaneLabel`) rather than reusing the
  status-column one. The status-column `DragTarget` in `_StatusColumn`
  intentionally only accepts a card whose `parentId` already matches that
  swimlane — that's the existing invariant (a status drag can never
  reparent). The label's `DragTarget` does the opposite: it only accepts a
  card whose `parentId` does *not* already match, so dropping a card back
  onto its own lane's label is silently rejected rather than a no-op
  network call.
- **Any card is tappable to drill in, even ones with zero children** —
  there's no `hasChildren` flag on `WorkItemDto`/`WorkItemCard`, and adding
  one just to grey out childless cards would mean an extra fetch (or a
  denormalized count) for a purely cosmetic affordance. Drilling into a
  leaf item just shows an empty board under that breadcrumb, which is a
  legitimate (if temporarily boring) state — the user can already tell
  from the breadcrumb where they are and navigate back.
- Verified end to end against the dev Docker stack: seeded a second
  top-level swimlane and a grandchild under an existing card via the API,
  then in a browser: drilled into a card to see its own child as a new
  swimlane, navigated back via the "Board" breadcrumb, dragged a card from
  one swimlane onto another's label to reparent it (status column
  preserved), and confirmed via both a page reload and a direct API call
  that the new `parentId` persisted.

## Time-frame filter, not a sprint system (Milestone 7)

- **Deliberately not a sprint/iteration entity.** The design doc originally
  called this milestone "Sprint system," but this isn't a software
  development project, so there's no fixed-length cycle to model. Per
  explicit product direction, it's a plain from/to date filter over each
  work item's own optional `StartDate`/`EndDate` — nothing about the time
  frame itself is persisted; it's pure client-side view state
  (`BoardViewModel._filterStart`/`_filterEnd`), reset on reload.
- **Filter semantics are interval *overlap*, not containment**, with a
  missing bound (the item's or the filter's) treated as open-ended rather
  than excluding the item. An item with only a start date is presumed
  ongoing indefinitely (matches any frame at or after that start); an item
  with only an end date is presumed to have always been active up to it
  (matches any frame at or before that end). `BoardViewModel
  .matchesTimeFilter` implements this directly; see its tests for the
  open-ended-bound cases specifically — that behavior is easy to get
  backwards (containment vs. overlap look similar for fully-bounded items
  and only diverge once one bound is missing).
- **`WorkItem.StartDate`/`EndDate` are set through their own operation,
  `Reschedule`** (`POST /work-items/{id}/schedule`), independent of
  `ChangeStatus`/`Reparent`/`Create` — same one-operation-per-concern
  pattern as the rest of `WorkItemsController`. The one validation rule
  (`StartDate` must not be after `EndDate`, when both are set) lives in
  `WorkItemService.RescheduleAsync` and is checked *before* the entity
  lookup, so it also rejects a bad range for a nonexistent id rather than
  a confusing 404 masking the real problem.
- **Added a minimal "set schedule" affordance directly on `BoardCard`**
  (a calendar icon opening a small dialog) even though a fuller work-item
  editing UI belongs to Milestone 9. Without *some* way to set these dates
  from the app itself, the filter would only ever be testable by calling
  the API directly — the dialog only edits start/end, nothing else, to
  avoid creeping into Milestone 9's scope.
- **Fixed a real 1px `RenderFlex` overflow** in `_StatusColumn` while
  verifying this in a browser: adding a second line (the schedule label)
  to `BoardCard` made cards just tall enough to overflow the height
  `IntrinsicHeight` computes for a status column (it keeps every column in
  a swimlane row the same height, and its intrinsic-height calculation can
  land a fraction of a pixel short of what the column's own content needs
  at layout time). Wrapped the column's card list in a
  `SingleChildScrollView` — invisible when content fits, and a genuine
  usability improvement for a swimlane with many cards in one column,
  which had no scroll affordance before this.
- Verified end to end against the dev Docker stack: the existing seeded
  items got real schedules via `POST .../schedule` (including one
  migrated from before this milestone, confirming the new nullable
  columns default to `null` on existing rows rather than requiring a
  backfill), then in a browser confirmed cards render their schedule
  label, the from/to filter correctly hides an item whose window ended
  before the filter's start while leaving unscheduled and in-range items
  visible, and the schedule dialog opens and is editable from the card
  itself.

## Search, status filter, and sort (Milestone 8)

- **Entirely client-side, no backend changes.** Same reasoning as the
  time-frame filter: the board already fetches each swimlane's full child
  list up front, so search (title/description substring), column
  visibility, and sort are just additional view-state predicates/
  comparators applied in `BoardViewModel`/`board_view.dart` over data
  already in hand. Scope was confirmed with the user first, since unlike
  Milestone 7, `project_design.md` had no dedicated section spelling out
  what this milestone should mean (it was just a one-line milestone-table
  entry) — search matches title *and* description, filter is a per-status
  column show/hide (not the search bar itself), sort is display-only
  (`Manual` = today's Rank order, plus Title/Start date/Due date) and
  never touches the backend `Rank` drag-and-drop relies on.
- **`WorkItemCard` gained a `description` field** purely so the frontend
  can search it — `WorkItemDto` already carried `Description`, it just
  wasn't mapped into the board's card model before this.
- **Sort comparator is `null` for `CardSortOption.manual`**, not a
  same-order comparator, and `_StatusColumn` skips sorting entirely when
  it's null rather than calling `.sort()` with one. Dart's `List.sort` is
  not guaranteed stable, so sorting with an always-0 comparator could
  visibly reshuffle the Rank-derived order; leaving the already-Rank-
  ordered list untouched is the only way to guarantee "Manual" actually
  means manual.
- **Hit a real hot-reload limitation verifying this in the dev container**,
  worth knowing for future frontend milestones: renaming/adding a field on
  a widget class with a `const` constructor (here, `_SwimlaneRow`/
  `_StatusColumn` picking up `cardComparator` and renaming
  `cardMatchesFilter`→`cardVisible`) is a shape change the Dart
  incremental compiler can't hot-*reload* — `flutter run`'s auto-reload-
  on-save (`docker/frontend/dev-entrypoint.sh`'s inotify→fifo trick) kept
  "succeeding" per its own log line while actually leaving the served app
  on stale code, because each reload attempt silently failed
  (`Hot reload rejected due to unsupported changes`) with no client
  connected to surface the error to. A same-shape-change hot *restart*
  (capital `R`) is required instead; since the entrypoint script only ever
  sends lowercase `r`, this needs a manual
  `docker compose exec -T frontend sh -c 'echo "R" > /tmp/flutter-stdin'`
  after any const-widget field change, or `docker compose attach frontend`
  and pressing `R` by hand.
- Verified end to end against the dev Docker stack: seeded two more cards
  via the API (one with a description containing a word its title
  doesn't) and reordered them so Rank order and alphabetical order
  differ, then in a browser confirmed searching by a description-only
  word filters down to just that card, unchecking a status chip hides
  that entire column (and only that column), and switching the sort
  dropdown to "Title (A–Z)" visibly reorders the cards while "Manual"
  restores the original Rank order.

## Authentication (Milestone 10)

- **JWT access token (15 min) + rotating opaque refresh token (30 days,
  `JwtOptions.RefreshTokenLifetime`).**
  Only the refresh token's SHA-256 hash is ever stored (`RefreshToken
  .TokenHash`) — a database read alone can never yield a usable
  credential. Every `RefreshAsync` call revokes the token it was given and
  issues a new one (`ReplacedByTokenHash` records the chain).
- **Reuse detection revokes the chain, not the account.** Presenting an
  already-rotated token means two parties hold it, so `RefreshAsync`
  walks `ReplacedByTokenHash` forward and revokes every token issued
  from it. The user's other sessions (separate logins) are untouched.
  Logged-out tokens have no replacement and are just rejected.
- **There's a 30-second grace window.** A rotated token that comes back
  within 30 s is rejected with a 401 but doesn't trigger chain
  revocation. The Flutter client still fires parallel refreshes with the
  same token when the access token expires (F-H1), and without the
  window every such page load would also revoke the fresh session
  server-side. Reconsider the window once F-H1 is fixed.
- **`RefreshToken.Version` maps to `xmin`**, so two simultaneous
  refreshes with the same token can't both mint a new pair: the loser's
  `SaveChanges` fails and it gets a 401. The migration adding it is
  SQL-free (Npgsql never creates the `xmin` system column); only the
  model snapshot changes. Like `WorkItem.Version`, the in-memory test
  provider doesn't generate `xmin`, so the race was verified against
  real Postgres.
- **Expired refresh tokens are purged per user whenever new ones are
  issued**, instead of by a background job. Revoked-but-unexpired tokens
  are kept, because reuse detection needs them.
- **Passwords are hashed with `Microsoft.AspNetCore.Identity`'s
  `PasswordHasher<User>`** (PBKDF2-HMAC-SHA256, framework-managed
  iteration count) via the standalone `Microsoft.Extensions.Identity.Core`
  package — deliberately not the full ASP.NET Core Identity system
  (roles/claims/EF schema), which would have imposed far more shape than
  a plain username+password table needs.
- **Login failures are uniformly `InvalidCredentialsException`** — unknown
  username, wrong password, and a locked-out account all produce the same
  generic "Invalid username or password," specifically so a caller can't
  use the error to enumerate valid usernames or learn an account's
  lockout state.
- **`[Authorize]` is the default for every endpoint**, via
  `AddAuthorization(options => options.FallbackPolicy = ...
  RequireAuthenticatedUser())` in `Program.cs`, rather than annotating
  each controller — a new controller is protected automatically instead
  of by remembering to add the attribute. `AuthController` opts individual
  actions out with `[AllowAnonymous]` (register/login/refresh/logout).
  **Controller-level `[AllowAnonymous]` was tried first and was a real
  bug**: in ASP.NET Core, `[AllowAnonymous]` anywhere in the chain wins
  unconditionally — it does *not* work like normal attribute precedence
  where the closer one (here, action-level `[Authorize]` on `Me`) wins.
  The compiler's own `ASP0026` warning caught this; per-action
  `[AllowAnonymous]` on just the four anonymous endpoints is correct.
- **`options.MapInboundClaims = false` is required on `AddJwtBearer`.**
  Without it, the handler silently remaps standard claim types (`sub` →
  a legacy XML-namespace URI) on the resulting `ClaimsPrincipal`, so
  `ClaimsPrincipalExtensions.GetUserId`'s `JwtRegisteredClaimNames.Sub`
  lookup would never match a real token — even though a unit test
  building a `ClaimsIdentity` directly (bypassing the real handler)
  wouldn't catch this, since it never goes through the remapping. Only
  manual `curl` verification against the running container caught it.
- **Enums must serialize as their string name, not the default int** —
  `Program.cs` registers a global `JsonStringEnumConverter`. Found by
  manually exercising registration end to end: the backend call succeeded
  (200, valid body), but the frontend's `user['kind'] as String` cast
  threw on the raw int, surfacing as a generic "could not create account"
  error that gave no hint the server had actually succeeded. This was
  latent already for `StatusDto.Category` (nothing parsed it client-side
  yet) — worth checking again for `WorkItemPriority` once that exists.
- **CORS narrowed from any-origin to a configured allowlist**
  (`Cors:AllowedOrigins`, no default — same fail-loud shape as
  `POSTGRES_PASSWORD`) now that there's something worth protecting; see
  the `Program.cs` comment left in Milestone 5 anticipating exactly this.
- **Frontend: the default (unnamed) `http.Client` DI binding is
  `AuthHttpClient`**, a wrapper that attaches the current access token
  and refreshes proactively before every call — every existing repository
  (`ApiBoardRepository` etc.) got authenticated for free with zero changes
  of its own, since they already depended on the *interface*
  `http.Client`, not a concrete type. `ApiAuthRepository` is the one
  exception, wired to a separately-`@Named('rawHttpClient')`
  unauthenticated client — it must never go through the wrapper, since
  refresh is what that wrapper would otherwise recurse into.
- **Refresh token persistence goes through `flutter_secure_storage`**,
  wrapped in try/catch at the `AuthSessionStore` level (not inside the
  store itself) so a persistence failure can never prevent the in-memory
  session state from updating or notifying listeners — `setSession`/
  `clear` update state and call `notifyListeners()` *before* attempting
  to persist, treating storage as strictly best-effort. This is exactly
  what caught a real bug during verification (next point).
- **A newly-added plugin needs a fresh `flutter run` process, not just a
  hot reload/restart.** `flutter_secure_storage`'s web plugin registration
  happens at process-start build-generation time; hot-restarting the
  already-running dev process after `flutter pub add` left it permanently
  throwing `MissingPluginException` on every secure-storage call. Because
  `AuthSessionStore.clear()`'s old implementation awaited the storage
  write *before* `notifyListeners()`, this silently broke logout (state
  cleared internally, UI never rebuilt) — found only by capturing browser
  console errors during interactive verification, not by pixel-diffing
  screenshots. Fixed both the ordering (see previous point) and confirmed
  the general lesson: `docker compose up -d --build <service>` (full
  recreate) after adding a plugin, not just triggering the fifo's hot
  reload.
- **`docker/frontend/dev-entrypoint.sh`'s `mkfifo` needed `rm -f` first.**
  `docker compose restart` (as opposed to recreate) reuses the container's
  writable layer, so `/tmp/flutter-stdin` from the previous run is still
  there and `mkfifo` refused to clobber it, crash-looping the container.
  Same fix pattern as the frontend/backend "needs a real restart, not
  reload" lessons above — this makes plain `restart` actually work instead
  of requiring a full recreate every time.
- **The debug banner occupies the same top-right corner Material AppBars
  conventionally put actions in**, and its hit-test region swallowed
  clicks meant for the new sign-out button during headless verification —
  set `debugShowCheckedModeBanner: false` on `MaterialApp` (also just a
  reasonable permanent choice; it's stripped from profile/release builds
  regardless).
- **`document.body.innerText` is useless for verifying Flutter web
  state** — reconfirms the canvas-rendering note from Milestone 3, but
  concretely: an automated check using it read "false" (assumed failure)
  for a flow a same-run screenshot proved had actually succeeded. Screenshots
  (or, for finding a specific element's real position, enabling semantics
  via the `[aria-label="Enable accessibility"]` placeholder and reading
  the resulting `flt-semantics` DOM) are the only reliable signal.
- Verified end to end against the dev Docker stack: registered a new
  account through the actual login UI (not just the API) and confirmed
  it lands on the board with real, authenticated API calls succeeding;
  clicked sign-out and confirmed it returns to the login screen; signed
  back in with the same credentials; and confirmed a full page reload
  (simulating an app restart) restores the session from the cached
  refresh token straight to the board with no re-login needed.

## Work-item details (Milestone 9)

- **`WorkItemLayer` is a plain lookup table** (`Id`, `Name`, `Order`),
  seeded with Project/Goal/Task, exactly mirroring `Status`'s own
  seeding pattern (`HasData` in the entity configuration, a unique index
  on `Order`). `WorkItem.LayerId` is nullable and carries no parent/child
  validation whatsoever — see the "Open decisions" note in
  project_design.md for why (parent/child stays fluid on purpose).
- **`Priority` is a plain enum on `WorkItem`** (`Low`/`Medium`/`High`/
  `Urgent`), not a lookup table like layers — only layers were asked to
  be configurable.
- **Title/Description/Layer/Priority share one endpoint**
  (`PUT /work-items/{id}/details`), separate from Status/Parent/Schedule/
  Assignee, which each keep their own — these four don't carry the same
  cross-cutting-invariant risk that justifies keeping Status and Parent
  apart (a shared endpoint here can't accidentally reparent or restatus
  anything), so grouping them doesn't weaken any guarantee.
- **A migration's auto-generated column default for a new enum column is
  the enum's first declared member (ordinal 0), not whatever default the
  C# property itself declares.** `WorkItem.Priority`'s C# default is
  `Medium`, but the generated migration backfilled every pre-existing
  row with `defaultValue: 0` — which is `Low` (`Low` is declared first in
  the enum). Caught by checking existing seeded items in a browser after
  applying the migration and seeing "Low" where "Medium" was expected.
  Fixed by hand-editing the migration's `defaultValue` to `1` before
  committing it (never applied anywhere but local dev) and correcting
  the already-backfilled rows directly. Worth checking again for any
  future enum column.
- **The detail screen reuses `BoardCard`'s schedule dialog** rather than
  duplicating it — extracted into `board/widgets/schedule_dialog.dart`
  as a public `showScheduleDialog()` function once a second caller
  actually needed it (not preemptively).
- **`WorkItemDetailRepository` is its own repository**, separate from
  `BoardRepository`, even though both ultimately call `/api/work-items/*`
  — the detail screen's concerns (full record, layers, users, per-field
  saves) are different enough from the board's (swimlane/column
  assembly, drag-and-drop) that folding them into one interface would
  have blurred both.
- Verified end to end against the dev Docker stack: opened an existing
  card's details via its new info icon, confirmed the Layer and Priority
  dropdowns are populated from the real `/api/work-item-layers` and enum
  values, changed both, saved, and confirmed via the actual PUT response
  (and a page reload) that the change persisted — this is what caught
  the migration default bug above, since the "before" state showed every
  existing item as Priority "Low".

## Tags (pre-Milestone 14 frontend round)

- **First scalar-list field on `WorkItem`.** Every prior collection-typed
  property was a real EF relationship (`Children`); `Tags` is a plain
  `List<string>`, mapped straight to a Postgres `text[]` column (Npgsql
  supports this natively, no join table). No index was added — tag
  search/filter is entirely client-side (same as every board filter since
  Milestone 8: the board already fetches full child lists, nothing queries
  by tag server-side), so a GIN index would be speculative.
- **Tags are set as a whole, not incrementally.** `SetTagsAsync` (mirroring
  `AssignAsync`'s "set or clear" shape) always replaces the full list; there
  is no add/remove-one-tag endpoint. The frontend always sends its complete
  current tag list.
- **Server-side normalization**: each tag is trimmed and rejected if blank
  (`InvalidWorkItemTagException`, 400), and the list is de-duplicated
  case-insensitively (first occurrence's casing wins) — this runs
  server-side rather than trusting the frontend, the same reasoning as
  `RescheduleAsync` validating its own dates itself. A work item can
  have at most 20 tags (counted after de-duplication) of at most 50
  characters each (`WorkItem.MaxTags`/`TagMaxLength`); `text[]` has no
  limit of its own, so without these there was no bound at all.
- **The generated migration (`AddWorkItemTags`) needed a hand-added
  `defaultValueSql: "ARRAY[]::text[]"`.** EF's `dotnet ef migrations add`
  left the new `NOT NULL text[]` column with no default at all, which would
  have failed against any pre-existing row — the same class of "always
  check the generated default" gotcha flagged for the Priority enum column
  back in Milestone 9, just for a different reason (no default inferred at
  all, rather than the wrong one).

## Comments and links (Milestone 11)

- **A `WorkItemLink` row is symmetric by construction, not by query
  trickery**: `ListForWorkItemAsync` matches on `WorkItemId == id OR
  LinkedWorkItemId == id`, and `WorkItemLinkDto.FromEntity` picks
  whichever side *isn't* the requesting item as "the other side." Create
  rejects a duplicate in either direction (`(a,b)` blocks a later
  `(b,a)`) and a self-link outright. Verified via `curl`: creating a link
  from item A to item B, then listing from *B*'s perspective, correctly
  returns A as the linked item under the same link id.
- **Both `WorkItemLink` foreign keys use `Restrict`, not `Cascade`.**
  Two `Cascade`/`SetNull` paths from the same table to the same
  principal is what EF Core rejects as "multiple cascade paths" — two
  `Restrict` paths are always fine, since neither one triggers an
  automatic delete. Because of that, `WorkItemService.DeleteAsync`
  removes every link touching the deleted item (or any cascaded
  descendant) itself, in the same `SaveChanges` — without that, Postgres
  rejected the delete and the client got an unmapped 500. The EF
  in-memory provider doesn't enforce FKs, which is why no test caught it
  originally; this was verified against a real Postgres container.
- **Deleting a board's scope item (or an ancestor of it, via cascade) is
  rejected** with `WorkItemIsBoardScopeException` (409), rather than
  deleting the board or clearing its `ScopeItemId`. A board is a saved
  view other users rely on; silently widening it to top-level or removing
  it would be a surprising side effect of deleting a card.
- **Comment edit/delete authorization is enforced in `CommentService`,
  not the controller** — `UpdateAsync`/`DeleteAsync` take the requesting
  user's id (from the access token, via `User.GetUserId()` in
  `CommentsController`) and throw `CommentAuthorMismatchException`
  (403) if it doesn't match the comment's author. No roles/admin
  concept exists yet to allow anyone else to moderate.
- **No work-item search/picker exists for adding a link** — the "add
  link" field on the detail screen takes the target's raw id, not a
  title search. Building a proper picker needs a general work-item
  search endpoint, which is arguably Milestone 8-shaped scope creep
  applied to a global (not board-visible-only) item set; flagged in
  project_design.md's decisions rather than built speculatively.
- Verified end to end against the dev Docker stack: added a comment
  through the actual API and confirmed it renders on the detail screen
  with the author's username, timestamp, and edit/delete controls (since
  it's the signed-in user's own comment); added a link between two work
  items and confirmed it appears in "Related work items" on the source
  item's screen with a remove-link control next to it.

## MCP (Milestone 13)

- **`BoardsController`/`StatusesController`/`WorkItemLayersController`
  were the only controllers left querying `WeaverDbContext` directly** —
  every other resource already had an `IXxxService`. This milestone's own
  rule ("MCP tools must call the same Application services used by the
  REST API," "MCP must never directly access EF Core") can't be satisfied
  for those three resources without a service to call, so
  `IBoardService`/`IStatusService`/`IWorkItemLayerService` were extracted
  first, mirroring `IWorkItemService`'s shape, and the controllers were
  switched over to them with no behavior change (see project_design.md's
  Milestone 13 decisions for the one exception: `BoardsController`'s
  invalid-`ScopeItemId` 404 now goes through `ApiExceptionMiddleware` like
  every other 404, instead of a bespoke plain-string body).
- **MCP tools live inside `Weaver.Api` itself** (`Mcp/` folder), not a
  separate project — they need the same `Weaver.Api.Contracts` DTOs
  controllers already use (`WorkItemDto`, `CommentDto`, etc.), and there's
  no `Weaver.Application` project for a "shared by both transports" layer
  to live in instead (see project_design.md's decision not to introduce
  one for this milestone). Each tool class mirrors one controller 1:1
  (`WorkItemTools` ↔ `WorkItemsController`, etc.) and takes the same
  service interface via constructor injection — the MCP C# SDK
  (`ModelContextProtocol.AspNetCore`) resolves a fresh tool instance per
  call from the same per-request DI scope a controller gets, so a
  `WeaverDbContext`-backed service behaves identically either way.
- **`list_all_work_items` was not added as an MCP tool.** When MCP was
  built, `IWorkItemService` had no `GetAllAsync` (it arrived later with
  the Hierarchy view), and adding one just for MCP would have meant MCP
  calling business logic REST doesn't have. `GetAllAsync` and
  `GET /work-items/all` are on `master` now, so that reason no longer
  holds; the tool just hasn't been added. Until it is,
  `list_work_item_children` (with `parentId` omitted for top-level) is
  what MCP exposes.
- **Tool-level auth is entirely "reuse the REST API's."**
  `app.MapMcp("/mcp")` is mapped in the same `Program.cs` pipeline as
  `app.MapControllers()`, after `UseAuthentication()`/`UseAuthorization()`,
  so it inherits the existing `[Authorize]` fallback policy with zero
  extra code — an MCP client sends `Authorization: Bearer <token>` from a
  normal `/api/auth/login` call, exactly like any REST client. No
  `[AllowAnonymous]`-equivalent exists for it. Confirmed via `curl`: a
  `tools/list` call with no bearer token gets a plain 401 before ever
  reaching MCP's own request handling.
- **Enum serialization needed an explicit fix, and it wasn't optional.**
  The MCP SDK's `WithToolsFromAssembly` takes a `JsonSerializerOptions`
  specifically to control tool parameter/result marshalling; without
  passing one, `WorkItemDto.Priority` etc. would serialize as a raw int
  (`1`) instead of the app's established `"Medium"` string convention
  (`JsonStringEnumConverter`, registered for MVC in Program.cs since
  Milestone 10). `Mcp/McpJsonSerializerOptions.cs` builds this by
  copy-constructing from `JsonSerializerOptions.Default`, not
  `new JsonSerializerOptions(...)` — the latter has no `TypeInfoResolver`,
  and the SDK calls `MakeReadOnly()` on whatever options it's given before
  first use, which throws `InvalidOperationException` at that point. This
  wasn't caught by any unit test (nothing exercises the DI container's
  actual object graph); it only surfaced as the entire API crashing on
  startup during end-to-end verification against the dev Docker stack,
  with the real exception buried at the bottom of a long `IServiceProvider`
  call-site-resolution stack trace pointing at `MapMcp`. Worth remembering
  for any future `JsonSerializerOptions` built for a DI-resolved SDK
  feature like this.
- **Domain exceptions are translated to `McpException`, not left to the
  SDK's default.** Without translation, any exception thrown from a
  service call (e.g. `EntityNotFoundException`) becomes a generic,
  message-free error `CallToolResult` — the SDK deliberately strips
  exception messages it doesn't recognize, to avoid leaking internal
  details by default. `Mcp/McpExceptionTranslation.cs` catches the same
  domain exception types `ApiExceptionMiddleware` already maps to HTTP
  statuses and rethrows them as `McpException(ex.Message, ex)`, whose
  message *is* propagated to the client by design (`McpException`'s own
  doc comment: "might be propagated to the remote endpoint; sensitive
  information should not be included" — safe here, since these are the
  exact same messages already sent in REST 404/409/400 bodies). Confirmed
  via a live `tools/call` against a nonexistent work item id: the client
  gets `"An error occurred invoking 'delete_work_item': WorkItem {id} was
  not found."` with `isError: true`, not a blank generic failure.
- **Stateless HTTP transport (`options.Stateless = true`), mapped at
  `/mcp` on the existing backend port.** No new Docker/compose config was
  needed — `compose.yaml`/`compose.override.yaml` already forward port
  8080 for the backend, and stateless mode means each MCP call is handled
  as an ordinary request with no server-side session state to persist
  between calls, matching how the REST API itself is already stateless
  (JWT-based auth, no server sessions).
- Verified end to end against the dev Docker stack (not just unit tests):
  registered a real user through `/api/auth/register`, used its access
  token to complete an MCP `initialize` handshake and `tools/list` call
  over the Streamable HTTP transport, confirmed `create_work_item`'s
  response showed `"Priority":"High"` (not `1`), confirmed
  `delete_work_item` against a nonexistent id came back as a
  client-visible `McpException` message rather than a crash or a silent
  generic failure, and confirmed a request with no bearer token was
  rejected with a plain 401 before reaching MCP's own handling at all.
