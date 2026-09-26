# Development notes

Practical knowledge about the dev environment and the traps it has.
Setup basics (`.env`, `docker compose up`, ports, running tests) are in
the [README](../README.md#local-development); design decisions are in
[architecture.md](architecture.md).

## Compose merge behaviour

`docker compose up` loads `compose.yaml` and then `compose.override.yaml`
on top. The merge is not a wholesale replacement:

- **`environment:` merges per variable.** Anything the override doesn't
  set is inherited from `compose.yaml`. This once left the dev backend in
  `Production`: the override didn't set `ASPNETCORE_ENVIRONMENT`, so the
  base file's value won, and a Compose-level `environment:` value always
  beats an image's `ENV` (the dev Dockerfile's `Development`). The
  override now sets it explicitly. **When you add a variable to a service
  in `compose.yaml`, decide whether the override needs its own value** —
  it will not inherit "nothing".
- **`ports:` is a union.** The override adds the backend's `8080:8080`,
  the frontend's `8082:8080` and Postgres's `5432:5432`; the base file's
  Caddy ports don't matter in dev because Caddy is disabled by profile.
- **`depends_on` conditions are replaced per dependency.** The override
  sets the frontend's backend dependency to `service_started`, since the
  first `dotnet watch` build can outlast the backend healthcheck.
- **Interpolation happens before profiles are applied**, so every
  `${VAR:?}` in `compose.yaml` must resolve even for services dev never
  runs. That's why `.env` needs non-empty placeholder values in dev, and
  why `WEAVER_DOMAIN` is checked by Caddy itself instead.
- To run only the production definition locally (e.g. to test built
  images), use `docker compose -f compose.yaml ...`.

## Frontend dev container

### Automatic hot reload

`flutter run` only hot-reloads on an `r`/`R` keypress; there's no built-in
file watcher outside an IDE. `docker/frontend/dev-entrypoint.sh` works
around this: `flutter run -d web-server` reads its stdin from a named pipe
(`/tmp/flutter-stdin`), and an `inotifywait` loop writes `r` to it whenever
`lib/` or `pubspec.yaml` changes. Container logs show `Performing hot
reload...` on each save.

- **The watcher only ever sends lowercase `r` (hot reload).** Some changes
  can't be hot-reloaded, e.g. adding, removing or renaming a field on a
  widget with a `const` constructor. The reload is then rejected
  (`Hot reload rejected due to unsupported changes`), but with no debugger
  client attached nobody sees the error, and the served app silently stays
  on old code. When the browser doesn't reflect a change, send a hot
  **restart**:

  ```sh
  docker compose exec -T frontend sh -c 'echo "R" > /tmp/flutter-stdin'
  ```

  or `docker compose attach frontend` and press `R` (detach with
  Ctrl-P Ctrl-Q). Reload the browser tab afterwards.
- **A newly added plugin needs a new `flutter run` process.** Plugin
  registration happens when the process builds, so after `flutter pub add`
  a hot restart keeps throwing `MissingPluginException` (this broke
  `flutter_secure_storage`, and with it logout, until the container was
  recreated). Recreate the container:

  ```sh
  docker compose up -d --build --force-recreate frontend
  ```

- **`docker compose restart frontend` works** too: the entrypoint runs
  `rm -f` before `mkfifo`, because a restart keeps the container's
  writable layer and the old fifo would otherwise make `mkfifo` fail and
  crash-loop the container.

### Running Flutter commands in the container

The dev container has the pinned Flutter 3.44.0, which is useful when the
host SDK differs (see [Local Flutter SDK](#local-flutter-sdk)):

```sh
docker compose exec frontend sh -c 'cd /app && flutter test'
docker compose exec frontend sh -c 'cd /app && flutter analyze'
docker compose exec frontend sh -c 'cd /app && dart format lib test'
```

`/app` is the bind-mounted `frontend/`. Anything that resolves packages
there (the entrypoint's own `flutter pub get`, or one you run) writes to
the shared mount: it can rewrite `frontend/pubspec.lock` and
`frontend/.dart_tool/` on the host. Check `git status` before committing
and don't commit lockfile churn you didn't intend.

### API base URL in dev

The Flutter dev server has no `/api` proxy, so the entrypoint passes
`--dart-define=API_BASE_URL=http://localhost:8080` (from the container's
`API_BASE_URL`) and the app appends `/api`. The browser, not the
container, makes the requests, which is why it's `localhost`.

## Backend dev container

- The backend runs `dotnet watch ... run` against the bind-mounted
  `backend/`, with `DOTNET_USE_POLLING_FILE_WATCHER=1` because file-change
  events are unreliable across Docker Desktop bind mounts.
- **`dotnet watch` can't hot-reload "rude edits"** such as changed
  interfaces, method signatures or new types in some positions. It then
  waits at a restart prompt that nobody in a detached container can
  answer, and the API keeps running old code. Restart the container:

  ```sh
  docker compose restart backend
  ```

- Dev uses `ASPNETCORE_ENVIRONMENT=Development` (so `/openapi/v1.json` is
  mapped) and hardcoded dev values for the connection string, JWT key and
  CORS origin (`http://localhost:8082`).
- Android/iOS builds run natively on the host and can use the dockerised
  backend at `http://localhost:8080` (`http://10.0.2.2:8080` from the
  Android emulator).

## Verifying Flutter web in a headless browser

Automated checks against the running web app (Playwright, Puppeteer,
etc.) behave differently from ordinary web pages:

- **Flutter renders to a `<canvas>`**, not DOM text. `waitForSelector
  ('text=…')` never matches and `document.body.innerText` comes back empty
  or misleading even on a fully rendered page; an automated check based on
  it once reported failure for a flow that had succeeded. **Use
  screenshots** as the signal.
- **To find an element's position**, enable semantics by clicking the
  `[aria-label="Enable accessibility"]` placeholder, then read the
  generated `flt-semantics` DOM.
- **Drag and drop needs raw coordinates**: `mouse.move` / `mouse.down` /
  `mouse.up` sequences. Element-based helpers like `dragAndDrop` have no
  draggable DOM elements to target.
- **`flutter run -d web-server` doesn't call `main()` until a debugger
  connects** (a DWDS handshake normally done by the Dart Debug extension
  or an IDE). A plain headless navigation sits on module loading forever
  with no console error. Call `window.$dartRunMain()` from the test script
  once the page has loaded to start the app. This doesn't apply to the
  production build served by nginx.
- **Capture console errors**, not just pixels. Some failures (e.g. the
  `MissingPluginException` above) leave the UI looking plausible.

## EF Core migrations

Create migrations from `backend/` with the EF tools, e.g.:

```sh
dotnet ef migrations add <Name> \
    --project src/Weaver.Infrastructure --startup-project src/Weaver.Api
```

They're applied automatically when the API starts. **Always read the
generated migration before committing**; the generator has been wrong
here more than once:

- **A new enum column's default is the enum's first member (ordinal 0),
  not the C# property's default.** `WorkItem.Priority` defaults to
  `Medium` in C#, but the generated migration backfilled existing rows
  with `0`, which is `Low`. Set `defaultValue` by hand.
- **A new `NOT NULL` array column got no default at all**, which fails on
  any existing row. `AddWorkItemTags` has a hand-added
  `defaultValueSql: "ARRAY[]::text[]"`.
- **Type conversions need hand-written SQL when a plain cast changes
  meaning.** `ScheduleDatesAsCalendarDates` converts `timestamptz` to
  `date` by rounding to the nearest UTC midnight rather than EF's
  `AlterColumn` cast, which would truncate in UTC and keep the one-day-off
  values. Write a `Down` that is lossless if possible.
- **Mapping `xmin` as a concurrency token produces a migration that runs
  no SQL.** The generated `AddColumn` for `xmin` is kept (it updates the
  model snapshot), but Npgsql never emits DDL for system columns. That's
  expected, not a broken migration.
- **InMemory tests won't catch migration or FK mistakes**; check new
  schema against the dev Postgres (`localhost:5432`, `weaver`/`weaver`).

## Local Flutter SDK

CI and both frontend Dockerfiles pin Flutter **3.44.0**. A different local
SDK causes real problems, not just warnings:

- An older SDK can fail `flutter pub get` outright (e.g.
  `flutter_secure_storage`'s dependencies need a newer Dart) and reports
  errors for newer Material APIs the code uses (`RadioGroup`,
  `DropdownButtonFormField.initialValue`).
- `dart format` output differs between SDK versions, so formatting with a
  different SDK can fail CI's format check or reformat unrelated files.

Install 3.44.0 (e.g. with `fvm`), or run tests and formatting in the dev
container as shown above.

`frontend/pubspec_overrides.yaml` currently pins `flutter_secure_storage`
to `^10.3.4` against `pubspec.yaml`'s `^11.2.0`. It was a local
workaround for an old SDK that got committed; removing it is F-L10 in the
code review.
