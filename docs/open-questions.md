# Open questions

Product and design decisions that are still open. Each entry states what
the system does today and what would need deciding. Settled decisions are
in [architecture.md](architecture.md) and [adr/](adr/); code-level defects
and refactors are tracked in [code_review_findings.md](../code_review_findings.md).

## Settled on 2026-11-02

These were open until the design review recorded in
[the Spaces and agents spec](../.scratch/spaces-and-agents/spec.md):

- **Registration is open:** self-registration is turned off and admins
  create every account; there are no per-Space permissions.
  ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md))
- **Agent users can't be created:** each agent runtime gets an agent
  account with scoped API keys, and writes carry an optional actor label.
  ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md))
- **MCP tools for admins:** agents can read admin data but never change
  it. ([ADR-0004](adr/0004-agent-accounts-and-api-keys.md))
- **Links are untyped, and added by raw id:** links become typed
  (related, blocks, duplicates) plus URL links, added through a search
  picker. ([ADR-0009](adr/0009-typed-links-and-dependency-flags.md))
- **Attachments:** wanted; stored under their own path so they can live on
  separate storage. (Milestone 19 in the spec.)
- **"Today" is the server's UTC date:** an admin sets one server-wide time
  zone. (Milestone 16 in the spec.)
- **Tag filter semantics:** the server-side query supports both OR and AND.
  ([ADR-0006](adr/0006-server-side-query-and-views.md))
- **Whether tags are per-Space:** tags stay global across Spaces; they
  aren't part of a Space's configuration.
  ([ADR-0001](adr/0001-spaces.md))
- **Items without dates have no Roadmap bar:** undated items live in the
  Backlog, and an undated parent is shown as a group row when a child
  matches. (Ticket 22.)
  ([ADR-0003](adr/0003-when-and-backlog.md))
- **Production deployment access model:** registration off, Tailscale-only
  reach; the published images are the product.
  ([ADR-0008](adr/0008-images-first-deployment.md))

## Accounts and access

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
  become a real problem. Agents moving to API keys removes the main
  accidental source of lockouts.

### Admin changes
- **Today:** other people's open apps keep their cached statuses until
  they reload, and a deactivated user's current access token keeps working
  for up to 15 minutes. Admin changes aren't recorded anywhere beyond
  their effect.
- **To decide:** whether any of these matter enough to address: pushing
  lookup changes to open clients, checking the user on every request, and
  an audit log of admin actions (work-item history is decided in
  [ADR-0005](adr/0005-change-history.md); admin actions aren't covered by
  it).

## Work items

### Comment moderation
- **Today:** only a comment's author can edit or delete it, admins
  included.
- **To decide:** whether admins (or item owners) should be able to
  moderate comments.

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
- **The Repeating tab ignores the header's search and filters.** To
  decide: whether it should apply them, or become a View.

## Tags

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
  server-side), possibly as part of a saved View.

### Roadmap quarter/year precision
- **Today:** Quarter and Year zoom levels use approximate 7- and 30-day
  columns, not true calendar weeks and months, so a bar can be a day or
  two off at those levels.
- **To decide:** whether calendar-aware bucketing is worth it.

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

Milestones 16–20 are planned in
[the Spaces and agents spec](../.scratch/spaces-and-agents/spec.md).
Milestone 14's remaining mobile work (safe areas, the keyboard covering
dialog fields, the back button, a full-screen detail view on phones) is
folded into Milestone 19. Hierarchy column resizing on touch is still
deferred, and iOS isn't a target yet: it would also need an App Transport
Security exception for plain-http dev servers.
