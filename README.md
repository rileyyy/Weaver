# Weaver

Weaver is a self-hosted work-item tracker. Work items form a tree of
arbitrary depth, and the main view is a swimlane board: each parent item is
a lane, its direct children are cards that move between status columns.
It ships as two container images (API and web app) plus PostgreSQL, and AI
agents can read and change work items through an MCP endpoint built into
the API.

## Features

- **Swimlane board with drill-down.** Cards move between status columns by
  drag and drop; a card can be dropped on another lane's label to reparent
  it. Tapping the current breadcrumb ("Board" at the top) picks a lane to
  drill into, as does "View sub-items" in a card's detail dialog, so its
  children become the lanes. Earlier breadcrumbs lead back up. Each card's
  left bar shows its layer's colour.
- **Hierarchy and Roadmap views.** Hierarchy shows every work item as a
  collapsible tree with resizable Number / Title / Status / Assigned To
  columns. Roadmap is a Gantt-style timeline (week, fortnight, month,
  quarter, year) with weekly gridlines and a today marker.
- **Time-frame filter.** A from/to range shows only items whose own
  start/end window overlaps it. A missing date is open-ended, not
  excluded.
- **Search, filter and sort.** Search matches title, description and tags.
  Status columns can be hidden, tags filtered, and cards sorted by title,
  start date or due date without touching the manual order.
- **Work-item details.** Title, description, layer (Project / Goal / Task,
  each with an admin-set colour),
  priority, assignee, status, schedule, sub-items, and a sequential `#N`
  number.
- **Tags, comments and links.** Free-text tags; comments editable by their
  author; symmetric "related to" links between work items.
- **Authentication.** Username/password accounts, short-lived JWT access
  tokens and rotating refresh tokens.
- **MCP tools for AI agents.** The same operations as the REST API, over
  Streamable HTTP at `/mcp`.

The next phase (Spaces, Types, Views, agent accounts, history and the
Trash) is planned in
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md).

## Tech stack and repo layout

| Path | What's there |
| ---- | ------------ |
| `backend/` | ASP.NET Core 9 API (`Weaver.Domain`, `Weaver.Application`, `Weaver.Infrastructure`, `Weaver.Api`), EF Core with PostgreSQL 16, NUnit tests. The MCP server is hosted inside the API. |
| `frontend/` | Flutter app (web in production; the board is built to work on phone widths too). |
| `docker/` | Production and dev Dockerfiles, the frontend's `nginx.conf`, and the Flutter dev entrypoint. |
| `compose.yaml` | An example deployment of the published images (Caddy, backups). |
| `compose.override.yaml` | Local-development overlay (build from source, hot reload). |
| `docs/` | Architecture, development notes and open questions (see [Documentation](#documentation)). |

## Local development

### Prerequisites

- Docker with Compose v2.
- Optional, for running tests outside the containers: the .NET 9 SDK and
  Flutter **3.44.0** (the version pinned in CI and the Dockerfiles; other
  versions format and analyze differently).

### Start the stack

```sh
cp .env.example .env
# Put any non-empty value in BACKEND_IMAGE, FRONTEND_IMAGE,
# POSTGRES_PASSWORD and JWT_SIGNING_KEY.
docker compose up
```

Compose validates `compose.yaml` against `.env` even in development, so the
required variables must be non-empty. The dev overlay ignores their values:
it builds both images from source and hardcodes its own database password
and signing key.

`docker compose up` picks up `compose.override.yaml` automatically. The
backend runs under `dotnet watch` and the frontend under `flutter run` with
automatic hot reload, both bind-mounting the working tree. Caddy and the
backup service are switched off in dev.

| Service | Address |
| ------- | ------- |
| Flutter dev server (the app) | http://localhost:8082 |
| API | http://localhost:8080 (e.g. `/api/statuses`, `/mcp`, `/health`) |
| PostgreSQL | `localhost:5432`, database/user/password `weaver` |

Sign in as `admin` with the password `development-only-admin-password`
(set in `appsettings.Development.json`); the backend creates that account
on an empty database. Nobody can register: admins create every other
account from the Admin page (the gear next to sign-out). See
[docs/development.md](docs/development.md) for hot-reload limits and other
dev-environment gotchas.

### Tests

Backend:

```sh
dotnet test backend/Weaver.sln
```

`Weaver.Api.IntegrationTests` starts a throwaway `postgres:16-alpine`
container through Testcontainers and runs the real HTTP pipeline against it,
so Docker must be running. To run only the fast unit tests:

```sh
dotnet test backend/Weaver.sln --filter "FullyQualifiedName!~IntegrationTests"
```

Frontend (from `frontend/`, with Flutter 3.44.0):

```sh
flutter test
dart format --output=none --set-exit-if-changed lib test
flutter analyze
```

CI runs all three frontend checks, so an unformatted file or an analyzer
warning fails the build. If your local Flutter isn't 3.44.0, run the tests
in the dev container instead:

```sh
docker compose exec frontend sh -c 'cd /app && flutter test'
```

## Production deployment

The published images are the product; `compose.yaml` is one example of
running them, with Caddy and its own certificate authority for a LAN. The
reference deployment instead runs the images under its own compose file
on a homelab (managed by Komodo, with Tailscale names and certificates
from tsdproxy) and is reachable over Tailscale only. See
[ADR-0008](docs/adr/0008-images-first-deployment.md).

Using the example compose file, the server needs only two files: `compose.yaml` and `.env`. **Never copy
`compose.override.yaml` to the server** — Compose would load it
automatically and switch to building from source. If it is present for
some reason, run every command as `docker compose -f compose.yaml ...`.

### Configure `.env`

Copy `.env.example` to `.env` and fill in every value. They ship empty on
purpose, so an unedited copy fails at startup instead of running with
publicly known secrets.

| Variable | Value |
| -------- | ----- |
| `BACKEND_IMAGE` | `ghcr.io/<owner>/weaver-backend` (`<owner>` lowercased) |
| `FRONTEND_IMAGE` | `ghcr.io/<owner>/weaver-frontend` |
| `WEAVER_IMAGE_TAG` | `latest` (default), a version such as `1.2.0`, or `sha-<commit>` |
| `WEAVER_DOMAIN` | The host name or IP clients use to reach the server, e.g. `weaver.lan` or `192.168.1.20`. Caddy issues the certificate for exactly this name. |
| `POSTGRES_PASSWORD` | `openssl rand -hex 24` (hex keeps it safe inside the connection string) |
| `JWT_SIGNING_KEY` | `openssl rand -base64 48`. The API refuses to start with a key under 32 bytes. |
| `WEAVER_FIRST_ADMIN_USERNAME` / `WEAVER_FIRST_ADMIN_PASSWORD` | The first admin account (see [The first admin](#the-first-admin)). The password needs at least 12 characters. |

Optional:

| Variable | Default | Purpose |
| -------- | ------- | ------- |
| `WEAVER_HTTP_PORT` / `WEAVER_HTTPS_PORT` | `80` / `443` | Host ports for Caddy, if those are taken. |
| `BACKUP_DIR` | `./backups` | Host directory for database dumps. |
| `BACKUP_HOUR_UTC` | `2` | Hour (UTC) of the nightly backup. |
| `BACKUP_KEEP_DAYS` | `7` | Days of daily dumps to keep. |
| `BACKUP_KEEP_WEEKS` | `4` | Weeks of Sunday dumps to keep. |
| `CORS_ALLOWED_ORIGIN` | `https://$WEAVER_DOMAIN` | Only needed if a web app on another origin calls the API directly. |

`WEAVER_DOMAIN` is checked when Caddy starts rather than by Compose, so a
missing value shows up as the `caddy` container exiting with a message.

### Start

```sh
docker compose pull && docker compose up -d
```

Startup is ordered: db → `migrate` (applies database migrations once, then
exits) → backend (reports healthy on `/health`) → frontend → Caddy. If a
migration fails, `migrate` exits non-zero and the backend doesn't start;
`docker compose logs migrate` shows why. The app is then at
`https://<WEAVER_DOMAIN>`.

### The first admin

Nobody can register: an admin creates every account, human or agent, from
the Admin page ([ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).
To get the first admin, set `WEAVER_FIRST_ADMIN_USERNAME` and
`WEAVER_FIRST_ADMIN_PASSWORD` in `.env` before the first start. When the
backend starts on a database with no accounts it creates that account as
an admin; once any account exists it ignores both values, so you can
remove them afterwards. If they are missing on an empty database, the
backend still starts but logs a warning, and nobody can sign in until you
set them and restart it (`docker compose up -d backend`).

Deployments that don't use the example compose file set the same thing on
the backend container as `FirstAdmin__Username` and `FirstAdmin__Password`.

### Update to a new release

```sh
docker compose pull && docker compose up -d
```

With `WEAVER_IMAGE_TAG=latest` this picks up the newest build from
`master`. To pin or roll back, set `WEAVER_IMAGE_TAG` to a version or
`sha-…` tag and run the same command. The `migrate` step applies the new
release's migrations before its backend starts; take a backup first if the
release changes the database.

### What's exposed

- Caddy owns host ports 80 and 443. It terminates TLS for everything (the
  app, `/api`, `/mcp`) and redirects HTTP to HTTPS.
- The backend publishes no port; it is only reachable through Caddy →
  nginx on the Compose network. `/health` is not proxied.
- PostgreSQL publishes no port.

### Trust the Caddy certificate authority

The example compose targets a LAN server with no public domain, so Caddy
issues its certificate from its own internal CA instead of Let's Encrypt.
Each device (and each MCP client) must trust that CA's root certificate
once.

The root lives in the `caddy-data` volume. Copy it out on the server:

```sh
docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./weaver-root-ca.crt
```

Then install `weaver-root-ca.crt` on each device:

- **macOS:** open it in Keychain Access, add it to the System keychain,
  open the certificate, expand **Trust** and set it to **Always Trust**.
- **Windows:** double-click it → **Install Certificate** → Local Machine →
  place it in **Trusted Root Certification Authorities**.
- **Linux:** copy it into the distro's CA store and refresh it (Debian/
  Ubuntu: `/usr/local/share/ca-certificates/` then
  `sudo update-ca-certificates`; Fedora/RHEL:
  `/etc/pki/ca-trust/source/anchors/` then `sudo update-ca-trust`).
  Firefox keeps its own store: Settings → Privacy & Security →
  Certificates → View Certificates → Authorities → Import.
- **iOS / iPadOS:** send the file to the device (AirDrop, mail) and install
  the profile under Settings → General → VPN & Device Management. Then
  enable full trust under Settings → General → About → **Certificate Trust
  Settings**.
- **Android:** Settings → Security → Encryption & credentials → Install a
  certificate → **CA certificate**. (Menu names vary by vendor.) Browsers
  honour user CAs; other apps may not, and the Weaver Android app doesn't.
  Point the app at an address with a publicly trusted certificate instead,
  such as Tailscale's `https://<machine>.<tailnet>.ts.net`.

The CA's keys exist only in the `caddy-data` volume. If the volume is
lost, Caddy creates a new root and every device has to trust the new one.

### Backups and restore

The `backup` service runs `pg_dump` from the official `postgres:16-alpine`
image: once at startup, then nightly at `BACKUP_HOUR_UTC`. Dumps go to
`$BACKUP_DIR/daily/` on the host; the Sunday dump is also copied to
`$BACKUP_DIR/weekly/`. Dailies older than `BACKUP_KEEP_DAYS` and weeklies
older than `BACKUP_KEEP_WEEKS` weeks are deleted. A dump is written as
`.partial` and renamed only when `pg_dump` succeeds, so a crash never
leaves a truncated file that looks like a backup. `docker compose logs
backup` shows each result.

To restore a dump (the backend is stopped so nothing writes during the
restore):

```sh
docker compose stop backend
docker compose exec backup pg_restore --clean --if-exists --no-owner \
    --dbname=weaver /backups/daily/<file>.dump
docker compose start backend
```

Copy `BACKUP_DIR` off the server as well (e.g. a scheduled `rsync` to
another machine). A dump on the same disk doesn't survive losing that
disk.

## Connecting an MCP client

- **Endpoint:** `https://<WEAVER_DOMAIN>/mcp`, Streamable HTTP transport,
  stateless (no session to keep between calls). In local dev it is
  `http://localhost:8080/mcp`.
- **Authentication:** the same as any API client. Log in with an existing
  account:

  ```sh
  curl -s https://<WEAVER_DOMAIN>/api/auth/login \
      -H 'Content-Type: application/json' \
      -d '{"username":"…","password":"…"}'
  ```

  and send the returned `accessToken` as `Authorization: Bearer
  <accessToken>` on every MCP request. There is no separate agent account
  type yet, so an MCP client acts as the human account it logs in with.
  Agent accounts with API keys replace this in Milestone 16
  ([ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)).
- **Access tokens last 15 minutes.** A long-running client has to log in
  again (or call `POST /api/auth/refresh` with the `refreshToken`) when
  requests start returning 401.
- **TLS:** the client (or the machine it runs on) must trust the Caddy
  root certificate, see above.

The tools cover work items, boards, statuses, layers, priorities, users,
comments, links and repeating items. Updates to details, schedule and tags
accept an optional `expectedVersion` (the item's `Version` from your last
read) to avoid overwriting someone else's change.

## Documentation

- [docs/architecture.md](docs/architecture.md): how the system is built and
  why, by area.
- [docs/development.md](docs/development.md): dev-environment details and
  gotchas (hot reload, headless browser checks, EF migrations).
- [docs/open-questions.md](docs/open-questions.md): product and design
  decisions that are still open.
- [docs/adr/](docs/adr/): architecture decision records, one decision per
  file.
- [GLOSSARY.md](GLOSSARY.md): the names Weaver uses for its concepts.
- [.scratch/](.scratch/): specs and tickets, one folder per feature; the
  current plan is
  [.scratch/spaces-and-agents/spec.md](.scratch/spaces-and-agents/spec.md).
- [CHANGELOG.md](CHANGELOG.md): what shipped, milestone by milestone.
- [project_design.md](project_design.md): the project's design rules
  (layering, board invariants, testing requirements).
- [code_review_findings.md](code_review_findings.md): the 2026-09-25 code
  review, closed apart from F-M7.

## Contributing

Read [CLAUDE.md](CLAUDE.md) before making changes. It sets the rules for
branching (`feature/` / `bugfix/` branches off `master`, never direct
commits), commits, testing and code comments, for human and AI
contributors alike.
