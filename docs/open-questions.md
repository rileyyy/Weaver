# Open questions

Product and design decisions that are still open. Each entry states what
the system does today and what would need deciding. Settled decisions are
in [architecture.md](architecture.md); code-level defects and refactors
are tracked in [code_review_findings.md](../code_review_findings.md).

## Accounts and access

### Registration is open
- **Today:** anyone who can reach the server can register with a username
  and password (`POST /api/auth/register`), and every account can read and
  change every work item. There is no invite, admin or per-board access
  control.
- **To decide:** whether registration should be gated (config flag,
  invite codes, first-user-is-admin) and whether access should ever be
  scoped per board or team. Fine for a single team on a LAN; not beyond
  that. (B-M6 in the code review.)

### Agent users can't be created
- **Today:** `User.Kind` has `Human` and `Agent`, but registration always
  creates `Human` and nothing else creates users. MCP clients log in with
  a human account's password and act as that person.
- **To decide:** how an automated agent gets its own identity: an API key
  or service-account flow, who can create one, and whether agent actions
  should be distinguishable in the UI.

### No password reset
- **Today:** users have no email address and there is no reset flow. A
  forgotten password can only be fixed in the database.
- **To decide:** an admin-driven reset, an email field plus self-service
  reset, or neither.

### Lockout and rate limiting
- **Today:** five wrong passwords lock an account for 15 minutes, which
  lets anyone lock out a known username; there is no rate limiting on
  `/api/auth/*`.
- **To decide:** whether to add rate limiting and change the lockout
  policy (B-M6).

### The dev signing key in `appsettings.json`
- **Today:** `backend/src/Weaver.Api/appsettings.json` contains a dev JWT
  signing key, so the startup check for a missing key never fires. The
  Compose deployment always sets `Jwt__SigningKey`, so it isn't affected,
  but running the image some other way without that variable would sign
  tokens with a key that is public in the repo.
- **To decide:** move the dev key and connection string to
  `appsettings.Development.json` (B-H1, deferred until production
  deployment).

## Work items

### Links are untyped, and added by raw id
- **Today:** a link is a symmetric "related to" association between two
  work items (not a URL, not an attachment), with no relationship types
  such as "blocks" or "duplicates". The detail dialog's "add link" field
  takes the target item's id; there is no search or picker, because there
  is no general work-item search endpoint.
- **To decide:** whether link types are needed, and whether to build a
  search endpoint so links can be added by title.

### Comment moderation
- **Today:** only a comment's author can edit or delete it. There are no
  roles.
- **To decide:** whether anyone (admins, item owners) should be able to
  moderate comments; this depends on a roles concept that doesn't exist.

### Priority is a fixed enum
- **Today:** `Low` / `Medium` / `High` / `Urgent`, hardcoded like
  `StatusCategory`.
- **To decide:** whether priorities should be configurable like layers
  and statuses (a lookup table).

### Attachments
- **Today:** none. Milestone 12 (attachments) was skipped.
- **To decide:** whether they're wanted, and where files would be stored.

## Tags

### Tags are edited only in the detail dialog
- **Today:** cards, Hierarchy rows and Roadmap rows display tags but have
  no inline way to add one, unlike the assignee avatar, which is tappable
  everywhere. A tag editor (text field plus removable chips) had no
  obvious compact place on a card.
- **To decide:** whether quick inline tagging is worth adding.

### Tag filter semantics
- **Today:** the Filters dialog's tag filter is OR ("any selected tag
  matches"); an empty selection applies no tag filter.
- **To decide:** whether AND ("all selected tags") is needed, as an option
  or instead.

### Tag casing
- **Today:** the server de-duplicates a single item's tags
  case-insensitively, keeping the first spelling, and search is
  case-insensitive. The filter chip list also collapses casings across
  items, showing whichever spelling it saw first. But the tag *filter*
  itself matches exactly (`BoardViewModel.matchesTagFilter`), so if one
  item has "Urgent" and another "urgent", selecting the "Urgent" chip
  hides the second. There is no rename or merge UI to fix inconsistent
  spellings, short of removing and re-adding the tag on each item.
- **To decide:** whether tags should be canonicalised across items (and
  the filter made case-insensitive), and whether a rename/merge tool is
  needed.

## Views

### Hierarchy column layout isn't persisted
- **Today:** column widths (Number, Title, Status, Assigned To) are
  resizable by dragging header dividers, and lanes and tree nodes can be
  collapsed, but all of it resets on reload.
- **To decide:** whether layout should persist per user (locally or
  server-side).

### Hierarchy caret doesn't indent
- **Today:** only the title text indents with depth; the expand caret and
  number column stay at a fixed position so the fixed-width columns stay
  aligned with their headers.
- **To decide:** whether the caret should indent with the title (the usual
  file-tree convention), which needs a different column-alignment
  approach.

### Roadmap quarter/year precision
- **Today:** Quarter and Year zoom levels use approximate 7- and 30-day
  columns, not true calendar weeks and months, so a bar can be a day or
  two off at those levels.
- **To decide:** whether calendar-aware bucketing is worth it.

### Items without dates have no Roadmap bar
- **Today:** an item with neither a start nor an end date shows its row
  but no bar.
- **To decide:** whether such items need some marker, or a way to schedule
  them from the Roadmap.

### Roadmap title/tags split
- **Today:** the Roadmap's fixed 260 px left column splits title and tags
  3:2. Packing by actual title width would need two-pass layout; the fixed
  ratio still ellipsises long titles and overflow-badges extra tags.
- **To decide:** revisit if that column becomes resizable like the
  Hierarchy columns.

### Unassigned placeholder on board cards
- **Today:** swimlane labels, Hierarchy rows and the assign dialog show a
  silhouette when an item is unassigned; board cards show nothing, to stay
  compact.
- **To decide:** whether cards should show it too.

## MCP

### No "list all work items" tool
- **Today:** MCP exposes `list_work_item_children` (top level when
  `parentId` is omitted) but no equivalent of `GET /work-items/all`. When
  the MCP tools were built the service had no `GetAllAsync`; it does now,
  so the tool simply hasn't been added.
- **To decide:** whether agents should get it, given `/all` is unpaged
  (B-M7).

## Roadmap items not yet started

- **Milestone 14, mobile refinement:** the board is built to stay usable
  on narrow screens, but the Hierarchy view's fixed columns overflow phone
  widths (F-M15) and no native mobile build is tested.
- **Milestone 15, production deployment:** the deployment pieces exist
  (TLS, backups, health-ordered startup, hardened images), but the
  remaining items flagged for it are open: B-H1 above, migrations applied
  on every startup rather than as an explicit step (B-M8), and no
  automated integration tests against Postgres (T-1).
