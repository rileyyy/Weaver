# Glossary

The words Weaver uses for its own concepts. Use these terms in code, issues,
docs and UI copy. The terms marked *planned* arrive in Milestones 16–19 (see
[the spec](.scratch/spaces-and-agents/spec.md)).

**Work item**
: Anything tracked, at any level: a renovation, a chore, a bug. Work items
form trees through their parent.
_Avoid:_ task, ticket, card. A card is how the Board draws a work item.

**Number**
: A work item's human-facing `#N`, unique across the server. It isn't the
item's id.

**Space** *(planned)*
: A top-level container such as Home, Village or Software. It holds a forest
of work-item trees, and it owns their statuses, priorities and Types. An
item belongs to exactly one Space.
_Avoid:_ project, workspace, board.

**Type** *(planned; replaces Layer)*
: A per-Space kind of work item (e.g. Room, Chore, Bug). It has a colour and
its own custom fields. Types don't limit what can be nested under what.
_Avoid:_ layer, work-item type.

**Custom field** *(planned)*
: A field a Type defines: text, Markdown, number, currency, date, checkbox,
single select, multi-select, user, or URL.

**Roll-up** *(planned)*
: An optional setting on a number or currency field. It shows on a parent
the sum of that field over the parent's descendants.

**Status category**
: To Do, Doing or Done. Every status has one. Done-category statuses record
a completion date and drive done-hiding.

**View** *(planned)*
: A saved, shared query that picks one or more Spaces, filters and a layout.
Each user picks a default View.

**Board**
: The swimlane layout of a View. Each parent is a lane and its children are
cards that move between status columns. It's no longer a stored entity.

**Overview** *(planned)*
: The built-in View of all scheduled and in-progress work across every
Space.

**Agenda** *(planned)*
: A list layout of dated work, suited to phones.

**Backlog** *(planned)*
: Every work item that isn't Done and has no dates. Time-framed layouts
don't show Backlog items.

**When** *(planned)*
: An optional free-text phrase on a work item ("before the Christmas market",
"after drywall"). It records the intent behind the item's dates.
**Resolving** a When sets the item's dates and/or adds a blocked-by link.
_Avoid:_ sprint, iteration, focus period.

**Link**
: A typed relationship between two work items (related, blocks/blocked by,
duplicates), which can cross Spaces. A **URL link** points outside Weaver
instead.

**Dependency conflict** *(planned)*
: A blocked item scheduled to start before its blocker ends. Weaver flags it
and never reschedules anything automatically.

**History** *(planned)*
: The append-only record of every change to a work item: who made it, when,
which field, and the old and new value.

**Trash** *(planned)*
: Where deleted work items wait until the retention period ends. Humans and
read-write agents can restore from it; only humans can purge it.

**Agent account** *(planned)*
: A user account for one agent runtime (e.g. "Claude Code (desktop)"). It
has no password and signs in with API keys.

**API key** *(planned)*
: A long-lived, revocable credential for an agent account. It has a label
and a scope: **read-only** or **read-write**.

**Actor label** *(planned)*
: Optional free text an agent sends with a write (e.g. "sub-agent:
test-writer"). History records it, so sub-agents that share one connection
can be told apart.
