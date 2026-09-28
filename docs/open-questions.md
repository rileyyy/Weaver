# Open questions

Product and design decisions that are still open. Each entry states what
the system does today and what would need deciding. Settled decisions are
in [architecture.md](architecture.md); code-level defects and refactors
are tracked in [code_review_findings.md](../code_review_findings.md).

## Accounts and access

### Registration is open
- **Today:** anyone who can reach the server can register with a username
  and password (`POST /api/auth/register`), and every account can read and
  change every work item. The first account on a database is an admin and
  admins can create, deactivate and delete accounts from the Admin page,
  but there is no invite or per-board access control.
- **Decided (2026-09-27):** kept open as an accepted risk while Weaver
  serves a single team on a trusted network. Revisit before exposing a
  deployment more widely: now that admins exist, the simplest gate is a
  config flag that turns self-registration off and leaves account creation
  to admins; invite codes are the alternative. Access could later be
  scoped per board or team. (B-M6 in the code review.)

### Agent users can't be created
- **Today:** `User.Kind` has `Human` and `Agent`, but registration always
  creates `Human` and nothing else creates users. MCP clients log in with
  a human account's password and act as that person.
- **To decide:** how an automated agent gets its own identity: an API key
  or service-account flow, who can create one, and whether agent actions
  should be distinguishable in the UI.

### No self-service password reset
- **Today:** an admin can set a new password from the Admin page, which
  also unlocks the account and signs it out everywhere. Users have no
  email address, so there is no self-service reset, and nobody can change
  their own password without an admin.
- **To decide:** whether users should be able to change their own
  password, and whether an email field plus self-service reset is wanted.

### Lockout policy
- **Today:** five wrong passwords lock an account for 15 minutes. The auth
  endpoints are rate-limited to 30 requests a minute per IP, which slows
  but doesn't stop someone deliberately locking out known usernames.
- **To decide:** whether lockout should be scoped per client (IP and
  username) or replaced by progressive delays, if deliberate lockouts
  become a real problem.

### Admin changes
- **Today:** other people's open apps keep their cached statuses until
  they reload, and a deactivated user's current access token keeps working
  for up to 15 minutes. Admin changes aren't recorded anywhere beyond
  their effect, and lookup and account changes are REST-only, not MCP
  tools.
- **To decide:** whether any of these matter enough to address: pushing
  lookup changes to open clients, checking the user on every request, an
  audit log of admin actions, and MCP tools for admins.

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
- **Today:** only a comment's author can edit or delete it, admins
  included.
- **To decide:** whether admins (or item owners) should be able to
  moderate comments.

### Attachments
- **Today:** none. Milestone 12 (attachments) was skipped.
- **To decide:** whether they're wanted, and where files would be stored.

### Repeating items
- **Lead time is fixed at seven days** (`WorkItemRecurrence.LeadDays`). To
  decide: whether it should be configurable per schedule.
- **Copies are dated on the occurrence day only** (start = end). A
  template spanning several days doesn't carry its duration over, and
  sub-items get the same single day. To decide: whether to preserve the
  template's duration and the sub-items' offsets.
- **Editing a schedule doesn't touch copies already created**, even ones
  dated after a new end date. To decide: whether edits should remove
  not-yet-started future copies.
- **"Today" is the server's UTC date.** To decide: whether a configured
  team time zone is needed.
- **The Repeating tab ignores the header's search and filters.** To
  decide: whether it should apply them.

## Tags

### Tag filter semantics
- **Today:** the Filters dialog's tag filter is OR ("any selected tag
  matches"); an empty selection applies no tag filter.
- **To decide:** whether AND ("all selected tags") is needed, as an option
  or instead.

### Tag casing
- **Today:** tags are case-insensitive everywhere: the server
  de-duplicates a single item's tags keeping the first spelling, and
  search, the filter chip list and the tag filter itself all ignore case.
  But different items can still store different spellings ("Urgent" on
  one, "urgent" on another), the chip shows whichever it saw first, and
  there is no rename or merge UI short of removing and re-adding the tag
  on each item.
- **To decide:** whether tags should be canonicalised across items, and
  whether a rename/merge tool is needed.

## Views

### Hierarchy column layout isn't persisted
- **Today:** column widths (Number, Title, Status, Assigned To) are
  resizable by dragging header dividers, and lanes and tree nodes can be
  collapsed, but all of it resets on reload.
- **To decide:** whether layout should persist per user (locally or
  server-side).

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

## Roadmap items not yet started

- **Milestone 14, mobile refinement:** the board is built to stay usable
  on narrow screens, but the Hierarchy view's fixed columns overflow phone
  widths (F-M15) and no native mobile build is tested.
- **Milestone 15, production deployment:** the deployment pieces exist
  (TLS, backups, health-ordered startup, an explicit migrate step,
  hardened images, integration tests against Postgres). What's left is
  deciding the access model before exposing a deployment beyond one team
  (see [Registration is open](#registration-is-open)).
