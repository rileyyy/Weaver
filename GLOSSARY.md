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

**Space**
: A top-level container such as Home, Village or Software. It holds a forest
of work-item trees. An item belongs to exactly one Space: a top-level item
is created in one, and children share their parent's. Reparenting never
crosses a Space: **moving** an item to another Space is its own step, which
takes its sub-items along and maps their statuses, Types and priorities onto
the new Space's. A Space owns its statuses, priorities and Types (a new one
starts with To Do / Doing / Done, Low / Medium / High / Urgent and Project /
Goal / Task); an item only takes its own Space's.
_Avoid:_ project, workspace, board.

**Type**
: A per-Space kind of work item (e.g. Room, Chore, Bug). It has a colour
and its own custom fields. Types don't limit what can be
nested under what. It replaced the global Layer (ticket 13).
_Avoid:_ layer, work-item type. (Code and API names such as `WorkItemType`,
`/api/work-item-types` and `list_work_item_types` keep the `work-item`
prefix, matching statuses and priorities.)

**Custom field**
: A field a Type defines: text, Markdown, number, currency, date, checkbox,
single select, multi-select, user, or URL. Only admins define them. An
item's values belong to its Type's fields, so changing the item's Type
clears them (history keeps them).
_Avoid:_ attribute, property.

**Roll-up**
: An optional setting on a number or currency field. A parent then shows
the sum of that field over its live descendants, next to its own value and
never including it. Computed when read, never stored. A currency field has
one currency, so a total is in that currency.
_Avoid:_ aggregate, subtotal.

**Status category**
: To Do, Doing or Done. Every status has one. Done-category statuses record
a completion date and drive done-hiding.

**Completion date**
: The server-calendar day a work item entered a Done-category status. Moving
between Done statuses keeps it; leaving Done clears it.

**Done-hiding period**
: Per Space, in days (default 14). The Board hides Done items completed longer
ago than this unless "show done" is on.

**View**
: A saved, shared query that picks one or more Spaces (or every Space),
filters and a layout. It keeps no time range: the window is chosen when it's
shown. Each user picks a default View, opened on launch.

**Breadcrumb**
: Where the board is: the open View (when it spans several Spaces), then a
Space, then each item drilled into.

**Board**
: The swimlane layout of a View. Each parent is a lane and its children are
cards that move between status columns. It isn't a stored entity (the old
Board entity was removed in ticket 11).

**Overview**
: The built-in View of all scheduled and in-progress work across every
Space: every item in a Doing-category status or with dates in the window.
It can't be changed or deleted, and it's the default View until a user
picks another.

**Agenda**
: A list layout of dated work grouped by day, suited to phones: Overdue,
then Today, then each later day. "Today" is the server's, in its time zone.
An item spanning several days shows once, with its range, under the first
day it's on from today on.

**Backlog**
: Every work item that isn't Done and has no dates. Time-framed layouts
don't show Backlog items. It has its own tab, grouped by parent.

**When**
: An optional free-text phrase on a work item ("before the Christmas market",
"after drywall"). It records the intent behind the item's dates.
**Resolving** a When sets the item's dates and/or adds a blocked-by link;
the phrase is kept. Until then the When is **unresolved**.

**Quick pick**
: A When the detail dialog offers as one tap (this weekend, next week, next
month). The server turns it into dates for today in the server time zone.
_Avoid:_ sprint, iteration, focus period.

**Tag**
: A free-text label on a work item. Tags are global: the same tag can be
used and filtered on across every Space.

**Link**
: A typed relationship between two work items, which can be in different
Spaces: *related*, *blocks* / *blocked by*, or *duplicates* / *duplicated
by*. A blocks link is stored once and each side reads it from its own view
(drywall *blocks* paint; paint is *blocked by* drywall). Two items have at
most one link. A **URL link** points outside Weaver instead (a PR, a quote,
a product page), with an optional label.

**Attachment**
: A file on a work item. The file is stored under the attachments path,
apart from the database, so it can live on other storage and must be
backed up separately. An item's attachments are kept while it is in the
Trash and removed when it is purged. Agents can list and fetch them but
not upload or delete them.
_Avoid:_ "upload" or "document" as a name for the attachment itself. "File"
is fine for the bytes it holds ("Attach file").

**Dependency conflict**
: A blocked item scheduled to start before its blocker ends. Weaver flags it
and never reschedules anything automatically. Starting on the blocker's end
date isn't a conflict, because the blocker can finish and the next item
begin on the same day, even though the Roadmap draws the end date as a full
day. An item missing either date, or either item being Done, is never a
conflict.

**History**
: The append-only record of every change to a work item: who made it, when,
which field, and the old and new value. It outlives a deleted item. Every
new field or operation on work items must record history too.

**Changes since T**
: The work items whose history has entries at or after a moment T, each
with those entries. Deleted items are included and marked. It's how an
agent asks "what changed since yesterday?".

**Summary**
: A work item in brief, with names instead of ids (number, title, Space,
status, dates, assignee, child count), as lists return it. Full detail comes
from the item itself.

**Trash**
: Where deleted work items wait until the retention period ends. Every
normal read leaves them out. Their comments, links and attachments are
kept until they're purged. Humans and read-write agents can restore from
it; only humans can purge it, and anything left past the retention period
is purged automatically.

**Agent account**
: A user account for one agent runtime (e.g. "Claude Code (desktop)"). It
has no password and signs in with API keys.

**API key**
: A long-lived, revocable credential for an agent account. It has a label
and a scope: **read-only** or **read-write**.

**Actor label**
: Optional free text an agent sends with a write (e.g. "sub-agent:
test-writer"). History records it, so sub-agents that share one connection
can be told apart.
