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
  concurrency token, so two people dragging cards at the same time get a
  conflict instead of a silent overwrite. This is why `WorkItemService`
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
