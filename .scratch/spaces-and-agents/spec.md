# Spaces, Types and agents: the next phase of Weaver

Status: ready-for-human

Agreed in a design review on 2026-11-02, after an audit of the code at
`e847af3`. This spec is the decision record for Milestones 16–20. The
individual decisions and their reasons are in [docs/adr/](../../docs/adr/),
and the vocabulary in [GLOSSARY.md](../../GLOSSARY.md). The work is broken
into tickets in [issues/](issues/), each listing the tickets that block it.
They're sliced vertically, so admin UI and MCP tools land with the feature
they belong to rather than in a later milestone.

## Context

- **Users:** Riley, Riley's fiancée, and AI agents. The main agent is Claude
  through the VS Code extension, plus smaller local models run at home
  later.
- **Reach:** the server is reachable over Tailscale only, never the public
  internet.
- **Use:** Weaver is for new work only. There's no Azure DevOps import.
- **Deployment state:** Weaver isn't running on the homelab yet, so schema
  and architecture changes carry no migration risk. Clean redesigns are
  preferred over compatibility shims.
- **Clients:** usage is expected to split about 60/40 web to phone. Both
  must work well.
- **Work it has to cover:** both software work and non-software work (home
  renovations, chores, village projects). Everything (statuses, Types,
  fields, retention) must be configurable by users in the UI, not in code
  or config files.

## What the audit found

**Agents.**
- Agents log in as a human, with that person's password, and must
  re-authenticate every 15 minutes.
- There's no server-side search, filter or `#Number` lookup, so agents page
  through every item.
- There's no actor or change history.
- `update_work_item_details` clears fields an agent leaves out.
- DTOs carry ids only, with no names.
- The MCP server has no instructions for agents.

**Humans.**
- Status can't be changed from the detail dialog.
- Sub-items can't be added from it either.
- Cards can't be reordered within a column.
- Done items are never hidden.
- The app reads only the first Board.
- Everything sits in one global tree with global lookups.
- There are no attachments, Markdown or typed links.

**Mobile.**
- No `SafeArea`.
- No keyboard-inset handling.
- No back-button handling outside the detail dialog.

**Ops.**
- The code review is closed apart from F-M7.
- CI runs both test suites, and the integration tests run against real
  Postgres.
- Deletes are permanent.

## Decisions

### People and access
- Self-registration is off; an admin creates accounts. There are no
  per-Space permissions, and every human sees and edits everything.
- Each agent runtime gets its own **agent account**, for example "Claude
  Code (desktop)" or "Ollama: llama-home". Agent accounts have no password
  and authenticate with **API keys**:
  - Keys are long-lived, revocable, stored hashed and shown once.
  - Each key has a label and a scope, **read-only** or **read-write**.
- Write tools take an optional **actor label** (e.g. "sub-agent:
  test-writer"). It tells apart sub-agents that share one MCP connection.
- Agents can't change admin settings or privileges. Server settings
  (time zone, trash retention), lookups and accounts are read-only to
  them. See [ADR-0004](../../docs/adr/0004-agent-accounts-and-api-keys.md).

### Spaces and Types
- **Spaces** (e.g. Home, Village, Software) are separate forests of
  drill-down trees.
- Each Space owns its statuses, priorities and **Types**, and the
  done-hiding period.
- Moving an item between Spaces is an explicit operation that remaps its
  status, Type and fields. Reparenting never crosses a Space boundary.
- **Types** replace Layers. Each Type defines **custom fields**, and
  number and currency fields can roll up as sums. Types put no limit on
  nesting. See [ADR-0001](../../docs/adr/0001-spaces.md) and
  [ADR-0002](../../docs/adr/0002-types-and-custom-fields.md).

### Views, time and planning
- **Views** are saved, shared server queries that can span Spaces and pick
  a layout: Board, Hierarchy, Roadmap or Agenda.
  - Each user picks a default View.
  - **Overview** is a built-in View of all scheduled and in-progress work
    across Spaces.
  - Views replace the Board entity.
- The Board and Roadmap show only items whose dates overlap the window.
  - A parent with no dates of its own appears when any of its children
    match.
  - A parent with its own dates shows its own bar.
- Items with no dates live in the **Backlog**.
- **When** is an optional free-text phrase such as "before the Christmas
  market" or "after drywall".
  - Resolving it sets dates and/or adds a *blocked-by* link.
  - You can type it, or pick a quick option (this weekend, next week, next
    month) that sets dates directly.
  - Weaver has no LLM of its own. Agents resolve phrases through MCP.
  - See [ADR-0003](../../docs/adr/0003-when-and-backlog.md).
- **Dependency conflicts** are flagged, and agents can query them, so a
  stale date can be raised with a human. Weaver never reschedules
  anything automatically.
- Entering a Done-category status records a **completion date**. The Board
  hides Done items older than the Space's period (default 14 days), with a
  "show done" toggle.
- The server has one configured **time zone**, set by an admin. It defines
  "today" for repeating items and filters.

### Item content
- Descriptions and comments render Markdown.
- `- [ ]` task lines in a description render as checkboxes you can tick.
  - Ticking edits the description with a version check.
  - This is the checklist and shopping-list feature, and it can be
    exported later.
- The detail dialog gets "add sub-item", plus editable status and parent.
- **Links** are typed: *related*, *blocks / blocked by* or *duplicates*.
  - There are also **URL links**, each with an optional label.
  - Links are added through a search picker and can cross Spaces.
  - See [ADR-0009](../../docs/adr/0009-typed-links-and-dependency-flags.md).
- **Attachments** are stored under a separate path, so the files can live
  on another storage device.
  - Images get thumbnails.
  - Phones can upload from the camera.
  - Agents can list and fetch attachments over MCP.

### History, deletes and the API
- An append-only **change history** records every change: who, when, which
  field, old value and new value. It records the account, key and actor
  label. See [ADR-0005](../../docs/adr/0005-change-history.md).
- Deletes go to the **Trash**, whose retention an admin sets.
  - Humans and read-write agents can view and restore the Trash.
  - Only humans can purge it.
  - See [ADR-0007](../../docs/adr/0007-trash.md).
- **One server-side query** serves Flutter, MCP and Views. It replaces the
  client-side filtering. See
  [ADR-0006](../../docs/adr/0006-server-side-query-and-views.md).
  - Inputs: Space, text, `#Number`, status category, assignee, tags (with
    OR or AND), Type, date window, updated-since and include-deleted.
  - It pages with a stable cursor.
- Field updates use **patch semantics**: a field left out is unchanged, and
  `null` clears it. Every write takes an optional `expectedVersion`.
  Status and parent changes stay as separate operations.

### Agent surface (MCP)
- Lists return lean summaries:
  - Number, title, Type and status names, dates, assignee name and child
    count.
  - Null fields are omitted.
- Detail comes from `get_work_item` and from `get_work_item_context`. The
  context tool takes an `include` list (ancestors, children, comments,
  links, history, …).
- Writes return the new version and the changed fields.
- Tools:
  - `whoami`
  - "changes since T"
  - unresolved When items
  - dependency conflicts
- Server instructions stay under about 500 tokens. Deeper documentation is
  an MCP resource that agents read on demand.

### Deployment
- **The published images are the product.** Riley's homelab runs them under
  its own compose, managed by Komodo, with TLS and Tailscale names from
  tsdproxy.
  - Images, environment variables and volumes are documented in one place.
  - The backend migrates on startup by default, with an environment
    variable to opt out.
- `compose.yaml` stays as a public example, including Caddy and the backup
  service. The backup service gets a staleness healthcheck.
- Backups are the homelab's job. The attachments path must be included in
  them. See [ADR-0008](../../docs/adr/0008-images-first-deployment.md).

## Milestones

Each milestone is several small branches off `master`.

### 16 — Foundations (backend and schema)
- Spaces with per-Space statuses, priorities and Types. The Board entity is
  removed.
- Custom fields and roll-ups.
- The When field, typed links and URL links.
- Change history with actor.
- The Trash, with configurable retention.
- Patch updates with `expectedVersion` on every write.
- The server-side query.
- Agent accounts, scoped API keys and actor labels.
- Registration off.
- The server time zone.
- Migrating on startup.

### 17 — Agent surface
- Lean summaries.
- `get_work_item_context` with `include`.
- `whoami`.
- Server instructions and the deeper resource.
- Queries for changes since T, unresolved When items and dependency
  conflicts.
- Trash list and restore over MCP.

### 18 — Human UX
- Space breadcrumb, and saved Views including Overview and Agenda.
- The Backlog.
- When input, with quick picks and free typing.
- Status and parent editable in the detail dialog, and "add sub-item".
- Reordering within a column.
- Done-hiding.
- Markdown with tickable task lists.
- The link picker and conflict badges.
- Roll-up display.
- The roadmap rule for parents with no dates.
- Admin UI for Spaces, Types, fields, trash retention, time zone, agent
  accounts and keys.

### 19 — Attachments, mobile polish and cleanup
- Attachments on a separate volume.
- `SafeArea`, keyboard insets and back-button handling, plus a full-screen
  detail view on phones.
- F-M7: stop rebuilding the whole board on every notify.
- Delete the merged branches.

### 20 — Later
- Reminders through ntfy.
- Exporting a task list (e.g. by email) through an agent.
- Autonomous and scheduled agents.

Once Milestones 16 and 17 are done, Weaver's own remaining work can be
tracked in Weaver by Claude. That's the best available test of the agent
experience.

## Still open

These items are in [docs/open-questions.md](../../docs/open-questions.md):
- Self-service password change.
- Lockout scope.
- An audit log of admin actions.
- Comment moderation.
- Tag casing and renaming.
- The details of repeating items (lead time, duration, edits).
- Persisting the Hierarchy layout.
- Roadmap calendar precision.
