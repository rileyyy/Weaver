# Weaver

Weaver is a self-hosted work-item tracker. Work items live in Spaces (such
as Home or Software) and form trees of arbitrary depth inside them, and the
main view is a swimlane board: each parent item is a lane, its direct
children are cards that move between status columns.
It ships as two container images (API and web app) plus PostgreSQL, and AI
agents can read and change work items through an MCP endpoint built into
the API.

## Features

- **Spaces.** Every work item belongs to one Space. The board shows one
  Space at a time and opens the last one used on that device; the switcher
  before the breadcrumbs changes it. Admins add and rename Spaces on the
  Admin page. An item can only be moved under an item in its own Space;
  "Move to Space…" in the detail dialog moves it, with its sub-items, to
  another Space, showing how its statuses, Types and priorities map first.
- **Swimlane board with drill-down.** Cards move between status columns,
  and up or down within one, by drag and drop, landing where they're
  dropped; a card can be dropped on another lane's label to reparent it. Tapping the current breadcrumb (the Space's name at the top) picks a lane to
  drill into, as does "View sub-items" in a card's detail dialog, so its
  children become the lanes. Earlier breadcrumbs lead back up. Each card's
  left bar shows its Type's colour.
- **Hierarchy and Roadmap views.** Hierarchy shows every work item in the
  Space as a collapsible tree with resizable Number / Title / Status /
  Assigned To
  columns. Roadmap is a Gantt-style timeline (week, fortnight, month,
  quarter, year) with weekly gridlines and a today marker. It shows the
  items dated in its window; a parent with no dates of its own is a group
  row without a bar.
- **Time-frame filter.** A from/to range shows only items whose own
  start/end window overlaps it. An item with only one date is open on the
  other side. An item with no dates at all matches no range: it's in the
  Backlog, and an undated parent still shows when an item under it matches.
- **Backlog.** Every item in the Space that isn't Done and has no dates,
  grouped by parent. "Set dates" gives one dates, which moves it out.
- **Search, filter and sort.** Search matches title, description and When;
  the tag filter matches tags, any or all of them.
  Status columns can be hidden, tags filtered, and cards sorted by title,
  start date or due date without touching the manual order.
- **Work-item details.** Title, description, Type (Project / Goal / Task by default,
  each with an admin-set colour),
  priority, assignee, status, schedule, sub-items, and a sequential `#N`
  number.
- **Tags, comments and links.** Free-text tags; comments editable by their
  author; typed links between work items (related, blocks, duplicates),
  across Spaces, and URL links to things outside Weaver.
- **Attachments.** Files attached to a work item from its detail dialog,
  to download or delete later. They're kept on their own storage path,
  not in the database. Agents can list and fetch them over MCP but not
  upload or delete them.
- **Markdown and checklists.** Descriptions and comments render Markdown.
  `- [ ]` task lines in a description are checkboxes you can tick, for
  checklists and shopping lists.
- **Trash.** Deleting a work item moves it and its sub-items to the
  Trash, from which they can be restored. Items are purged for good once
  they've been there longer than the retention period (30 days unless an
  admin changes it), and a person can purge one or empty the Trash by
  hand. Agents can list and restore the Trash but not purge it.
- **Change history.** Every change to a work item is recorded: who made it
  (agents are marked, with the key and actor label they used), when, and
  the old and new value. The detail dialog's History tab lists it, and it
  survives deleting the item.
- **Authentication.** Username/password accounts, short-lived JWT access
  tokens and rotating refresh tokens.
- **MCP tools for AI agents.** The same operations as the REST API, over
  Streamable HTTP at `/mcp`.

The next phase (Spaces, Types, Views and the Trash) is planned in
[the Spaces and agents spec](.scratch/spaces-and-agents/spec.md).

## Tech stack and repo layout

| Path | What's there |
| ---- | ------------ |
| `backend/` | ASP.NET Core 9 API (`Weaver.Domain`, `Weaver.Application`, `Weaver.Infrastructure`, `Weaver.Api`), EF Core with PostgreSQL 16, NUnit tests. The MCP server is hosted inside the API. `Weaver.Runner` is the optional agent runner, a separate worker that calls the API. |
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

### Deployment reference

Everything needed to run the images under any compose file. A minimal
deployment is three services, `db`, `backend` and `frontend`, plus the
environment and volumes below.

**Images**

| Image | Port | Notes |
| ----- | ---- | ----- |
| `ghcr.io/<owner>/weaver-backend` | 8080 | The API, including `/mcp` and `/health`. Runs as a non-root user. Its only state is the attachment files under `Attachments__Path`; everything else is in PostgreSQL. |
| `ghcr.io/<owner>/weaver-frontend` | 8080 | nginx (uid 101) serving the web app. It proxies `/api/` and `/mcp` to `http://backend:8080`, so the API must be reachable under the service name `backend`. Takes no configuration. |
| `postgres:16-alpine` | 5432 | The database. Any PostgreSQL 16 works. |
| `ghcr.io/<owner>/weaver-runner` | none | Optional. Does scheduled agent work with a model of your choice; see [The agent runner](#the-agent-runner-optional). |

`<owner>` is the GitHub account that publishes the images, lowercased.
Each Weaver image is tagged `latest` (newest `master` build), with a
version such as `1.2.0`, and `sha-<commit>`.

**Backend environment.** `__` separates ASP.NET Core configuration
sections.

| Variable | Required | Default | Purpose |
| -------- | -------- | ------- | ------- |
| `ConnectionStrings__Weaver` | Yes | — | Npgsql connection string, e.g. `Host=db;Port=5432;Database=weaver;Username=weaver;Password=…`. |
| `Jwt__SigningKey` | Yes | — | Signs access tokens. At least 32 bytes (`openssl rand -base64 48`); the API refuses to start without one. |
| `FirstAdmin__Username` / `FirstAdmin__Password` | On first start | none | The admin created when the backend starts on a database with no accounts; ignored once any account exists. The password needs at least 12 characters. See [The first admin](#the-first-admin). |
| `Attachments__Path` | No | `/data/attachments` | Directory holding attachment files, created on startup if missing. Mount a volume here and back it up with the database: Postgres holds each attachment's name and size, this directory holds the file. It's separate from the database so it can live on other storage. The image's user (`$APP_UID`, 1654) must be able to write to it. |
| `Attachments__MaxFileSizeMegabytes` | No | `25` | The largest attachment accepted, in MiB; a bigger upload gets 413. The frontend image's nginx caps `/api/` request bodies at 30 MiB, so raising this past about 29 also needs a larger `client_max_body_size` in front of the API (a replacement `/etc/nginx/conf.d/default.conf` mounted into the frontend container, or a rebuilt image). |
| `Database__MigrateOnStartup` | No | `true` | Applies pending migrations before serving. If a migration fails, the API exits with the reason in its log instead of serving against an old schema. Set `false` to run migrations as a separate step instead: the image's `migrate` command (e.g. `command: ["migrate"]`) applies them and exits. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` | Leave it as `Production`. `Development` maps the OpenAPI document and accepts the public development signing key and first-admin password. |
| `Cors__AllowedOrigins__0`, `__1`, … | No | none | Web origins allowed to call the API cross-origin. The web app calls it same-origin through nginx, so this is only for some other web origin. |
| `RateLimiting__Auth__PermitLimit` | No | `30` | Requests per client IP to the sign-in endpoints per window. |
| `RateLimiting__Auth__Window` | No | `00:01:00` | The rate-limit window. |
| `Jwt__AccessTokenLifetime` | No | `00:15:00` | How long an access token lasts. |
| `Jwt__RefreshTokenLifetime` | No | `30.00:00:00` | How long a refresh token lasts. |
| `Jwt__Issuer` / `Jwt__Audience` | No | `weaver-api` / `weaver-client` | Written into and checked on every token. Changing either signs everyone out. |
| `AllowedHosts` | No | `*` | Host names the API answers to. Leave it: requests reach the API through nginx. |
| `Logging__LogLevel__Default` | No | `Information` | Standard ASP.NET Core log levels. |

The image sets `ASPNETCORE_HTTP_PORTS=8080` and
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`; don't override them.

**Server settings.** Some settings live in the database rather than the
environment, and an admin changes them on the Admin page's Server tab:

| Setting | Default | Purpose |
| ------- | ------- | ------- |
| Time zone | `UTC` | The IANA time zone that defines "today", e.g. when a repeating item's next occurrence is due. |
| Trash retention | 30 days | How long a deleted work item stays in the Trash before it's purged for good, with its comments, links and attachment files. Counted from the moment it was deleted. A background job checks hourly. |

**Example compose (`.env`).** `compose.yaml` builds the services'
environment from these. The required ones ship empty in `.env.example` on
purpose, so an unedited copy fails at startup instead of running with
publicly known secrets.

| Variable | Required | Default | Purpose |
| -------- | -------- | ------- | ------- |
| `BACKEND_IMAGE` | Yes | — | `ghcr.io/<owner>/weaver-backend` |
| `FRONTEND_IMAGE` | Yes | — | `ghcr.io/<owner>/weaver-frontend` |
| `WEAVER_IMAGE_TAG` | No | `latest` | A tag from the images table. |
| `WEAVER_DOMAIN` | Yes | — | The host name or IP clients use to reach the server, e.g. `weaver.lan` or `192.168.1.20`. Caddy issues the certificate for exactly this name. Checked when Caddy starts rather than by Compose, so a missing value shows up as the `caddy` container exiting with a message. |
| `POSTGRES_PASSWORD` | Yes | — | `openssl rand -hex 24` (hex keeps it safe inside the connection string). |
| `JWT_SIGNING_KEY` | Yes | — | Becomes `Jwt__SigningKey`; `openssl rand -base64 48`. |
| `WEAVER_FIRST_ADMIN_USERNAME` / `WEAVER_FIRST_ADMIN_PASSWORD` | On first start | — | Become `FirstAdmin__Username` / `FirstAdmin__Password`. |
| `WEAVER_HTTP_PORT` / `WEAVER_HTTPS_PORT` | No | `80` / `443` | Host ports for Caddy, if those are taken. |
| `CORS_ALLOWED_ORIGIN` | No | `https://$WEAVER_DOMAIN` | Becomes `Cors__AllowedOrigins__0`. |
| `ATTACHMENTS_DIR` | No | `attachments` | Where the backend's attachment files live. A name is a Docker volume; a host path (`./…` or `/…`) puts them on other storage, and that directory must be owned by uid 1654. |
| `BACKUP_DIR` | No | `./backups` | Host directory for database dumps. |
| `BACKUP_HOUR_UTC` | No | `2` | Hour (UTC) of the nightly backup. |
| `BACKUP_KEEP_DAYS` | No | `7` | Days of daily dumps to keep. |
| `BACKUP_KEEP_WEEKS` | No | `4` | Weeks of Sunday dumps to keep. |
| `BACKUP_MAX_AGE_HOURS` | No | `36` | The backup service reports unhealthy once its newest dump is older than this. |

The example compose needs Docker Engine 25 or later: older engines ignore
the backup healthcheck's `start_interval`, so it reports healthy up to
five minutes after the startup dump instead of seconds after.

**Volumes**

| Volume (example compose) | Mounted at | Holds |
| ------------------------ | ---------- | ----- |
| `pgdata` | `db:/var/lib/postgresql/data` | The database. Whoever runs the images backs it up. |
| `attachments`, or `$ATTACHMENTS_DIR` (bind mount) | `backend:/data/attachments` | Attachment files. Back it up along with the database; the `backup` service doesn't. |
| `caddy-data` | `caddy:/data` | Caddy's local CA keys and certificates. |
| `$BACKUP_DIR` (bind mount) | `backup:/backups` | The backup service's dumps. |

### Using the example compose file

The server needs only two files: `compose.yaml` and `.env`. **Never copy
`compose.override.yaml` to the server** — Compose would load it
automatically and switch to building from source. If it is present for
some reason, run every command as `docker compose -f compose.yaml ...`.

Copy `.env.example` to `.env`, fill it in (see the table above), and
start:

```sh
docker compose pull && docker compose up -d
```

Startup is ordered: db → backend (applies database migrations, then
reports healthy on `/health`) → frontend → Caddy. If a migration fails,
the backend exits without serving (Compose keeps restarting it) and the
frontend doesn't start; `docker compose logs backend` shows why. The app
is then at `https://<WEAVER_DOMAIN>`.

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
`sha-…` tag and run the same command. The new backend applies the
release's migrations when it starts; take a backup first if the release
changes the database.

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

**The `backup` service only dumps PostgreSQL.** Attachment files live in
the `attachments` volume (or `ATTACHMENTS_DIR`), and nothing in the example
compose copies them; the service's healthcheck only watches the database
dumps. Back up the attachments path as well
([ADR-0008](docs/adr/0008-images-first-deployment.md)), for example with
the same scheduled copy that takes `BACKUP_DIR` off the server. Back both
up around the same time: a database restored from before an upload lists
no attachment for that file, and one restored from after an upload lists
one whose file is missing.

The `backup` service runs `pg_dump` from the official `postgres:16-alpine`
image: once at startup, then nightly at `BACKUP_HOUR_UTC`. Dumps go to
`$BACKUP_DIR/daily/` on the host; the Sunday dump is also copied to
`$BACKUP_DIR/weekly/`. Dailies older than `BACKUP_KEEP_DAYS` and weeklies
older than `BACKUP_KEEP_WEEKS` weeks are deleted. A dump is written as
`.partial` and renamed only when `pg_dump` succeeds, so a crash never
leaves a truncated file that looks like a backup. `docker compose logs
backup` shows each result.

The service turns unhealthy in `docker compose ps` (and in Komodo) when
its newest daily dump is older than `BACKUP_MAX_AGE_HOURS` (36 by
default), so a backup that keeps failing doesn't go unnoticed. It's
checked every 5 minutes; `docker inspect --format '{{json
.State.Health}}' <backup container>` shows the newest dump's age.

To restore a dump (the backend is stopped so nothing writes during the
restore):

```sh
docker compose stop backend
docker compose exec backup pg_restore --clean --if-exists --no-owner \
    --dbname=weaver /backups/daily/<file>.dump
docker compose start backend
```

Copy `BACKUP_DIR` and the attachments off the server as well (e.g. a
scheduled `rsync` to another machine). A dump on the same disk doesn't
survive losing that disk. With the default named volume, the files are
under the path `docker volume inspect <project>_attachments` reports;
setting `ATTACHMENTS_DIR` to a host directory makes them easier to copy.
To restore attachments, put the files back in that directory with the
backend stopped, owned by uid 1654.

### The agent runner (optional)

`weaver-runner` does agent work on a schedule without anyone starting it
([ADR-0010](docs/adr/0010-agent-runner.md)). Its one task kind so far,
`when-resolution`, turns unresolved When phrases into dates or blocked-by
links using a model you choose, e.g. a small local one in Ollama. Weaver
never calls a model: the runner reads and writes through the REST API
with an agent key like any other agent, and Weaver stores none of the
runner's settings.

For each unresolved item that isn't Done, the runner asks the model one
question (the item, its When, today's date and the quick picks in the
server's time zone, and a few items the phrase may be waiting for) and
checks the JSON answer before writing anything. It only sets dates on an
item that has none and only adds links; it never changes or removes
anything. A bad answer for one item, or a request Weaver or the model
turns down for that item alone (such as a prompt too long for the model's
context window), is logged and skipped, and the run carries on. If the
model or Weaver can't be reached, refuses its key or has a server error,
the run stops and the process exits with 1.

**Set it up.** On the Admin page's **Agents** tab, add one agent account
per model and task kind (e.g. "Runner: llama3.1-8b / When"), so history
tells the models apart, and issue it a **read-write** key. Then configure
the task kind, either in a JSON file mounted at `/config/runner.json`:

```json
{
  "Tasks": {
    "WhenResolution": {
      "ModelEndpoint": "http://ollama:11434/v1",
      "Model": "llama3.1:8b",
      "WeaverUrl": "http://backend:8080",
      "AgentApiKey": "wvr_…",
      "Cron": "0 3 * * *",
      "TimeZone": "Europe/Berlin"
    }
  }
}
```

or in the environment, e.g. `Tasks__WhenResolution__Model=llama3.1:8b`
(the environment wins over the file).

| Setting | Required | Default | Purpose |
| ------- | -------- | ------- | ------- |
| `ModelEndpoint` | Yes | — | Base URL of an OpenAI-compatible API; the runner calls `<ModelEndpoint>/chat/completions`. Ollama, LM Studio, vLLM and OpenAI all work. |
| `Model` | Yes | — | The model name the endpoint expects. |
| `ModelApiKey` | No | none | Sent as `Authorization: Bearer …` to the model endpoint, for hosted providers. |
| `WeaverUrl` | Yes | — | The API's base URL, e.g. `http://backend:8080` from the same compose network. |
| `AgentApiKey` | Yes | — | The agent's read-write key. |
| `Cron` | No | none | Five-field cron expression for when to run, e.g. `0 3 * * *` for 3am. Without one the task kind only runs on demand. |
| `TimeZone` | No | the container's (`TZ`, else UTC) | IANA time zone the cron expression is read in. |
| `JsonMode` | No | `true` | Asks the endpoint for a JSON-only reply (`response_format: json_object`). Set `false` if the endpoint refuses it. |
| `ModelTimeoutSeconds` | No | `300` | How long one answer may take. A timeout counts as the model being unreachable. |
| `ReplaceDatesWhenPhraseEdited` | No | `false` | `WhenResolution` only. Also resolve items whose When a person edited after their dates were set (shown as "awaiting re-resolution"), replacing those dates. Links are never removed. |

With no arguments the runner stays up and starts a run of each task kind
at its cron times. To run a task kind once and exit, for a test or from an outside
scheduler such as cron or Komodo, pass `--run when-resolution`:

```sh
docker run --rm -v ./runner.json:/config/runner.json ghcr.io/<owner>/weaver-runner --run when-resolution
```

Exit codes: `0` the run finished (some items may have been skipped), `1`
a run stopped because the model or Weaver couldn't be reached, `2` bad
arguments or configuration. In scheduled mode a stopped run also ends the
process with `1`, so give the service a restart policy (e.g. `restart:
unless-stopped`); it waits for the next scheduled time after a restart.

Each write's history carries the actor label `Runner when-resolution,
confidence <low|medium|high>`, the model's own confidence in that answer.

## Connecting an MCP client

- **Endpoint:** `https://<WEAVER_DOMAIN>/mcp`, Streamable HTTP transport,
  stateless (no session to keep between calls). In local dev it is
  `http://localhost:8080/mcp`.
- **Authentication: an agent account and an API key**
  ([ADR-0004](docs/adr/0004-agent-accounts-and-api-keys.md)). Give each
  agent runtime (e.g. "Claude Code (desktop)") its own agent account, so
  its changes can be told apart from yours. On the Admin page's **Agents**
  tab, add the agent, open it and choose **Issue key** with a label and a
  scope. The key (it starts with `wvr_`) is shown only this once, with a
  copy button; Weaver keeps just a hash of it. The scope is **read-only**
  (reads only; every write is refused) or **read-write** (what a non-admin
  person can do). No agent can change admin data (server settings,
  statuses, priorities, Types, accounts or admin rights), whatever its
  scope, and an agent account can't be made an admin. Send the key as
  `Authorization: Bearer <key>` on every MCP request. It doesn't expire, so
  there's nothing to refresh; revoke it on the same page, or deactivate the
  whole agent from the Agents tab (its keys stop working until it's
  reactivated). The same steps are available over the API under
  `/api/admin/agents` for scripting.
- **Keep the key out of the config file.** For Claude Code, a project's
  `.mcp.json` can read it from an environment variable:

  ```json
  {
    "mcpServers": {
      "weaver": {
        "type": "http",
        "url": "https://<WEAVER_DOMAIN>/mcp",
        "headers": {
          "Authorization": "Bearer ${WEAVER_API_KEY}"
        }
      }
    }
  }
  ```

  Set `WEAVER_API_KEY` in the environment Claude Code starts from. The
  `whoami` tool says which account, key and scope the connection is using.
- **Agent accounts have no password**, so they can't log in at
  `/api/auth/login`. A human's access token works on `/mcp` too, but it
  lasts only 15 minutes.
- **TLS:** the client (or the machine it runs on) must trust the Caddy
  root certificate, see above.

The tools cover work items, Spaces (`list_spaces`; agents can't change
them), statuses, Types, priorities, users, comments, links, repeating
items and attachments (`list_work_item_attachments` and
`get_work_item_attachment`; agents can't upload or delete them).
`get_work_item_context` returns an item (by id or `#Number`) with whichever
of its ancestors, children, comments, links, history and attachments you
`include`, in one call. A top-level item is created with a `spaceId`; a child joins its
parent's Space. Updates to details, schedule and tags
change only the arguments you send: leave one out to keep it, or send
`null` to clear it. Every work-item write accepts an optional
`expectedVersion` (the item's `Version` from your last read) to avoid
overwriting someone else's change, and returns the item with its new
version and the fields that changed.

Every tool that changes a work item also takes an optional `actorLabel`
(e.g. `"sub-agent: test-writer"`, at most 100 characters). History records
it with the change, so sub-agents sharing one key can be told apart. A REST
client can send the same thing as an `X-Actor-Label` header.

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
