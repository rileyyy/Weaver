# 47: Read a Space's history

**What to build:** Ticket 11 records a Space's creation and renames, and ticket 21 its done-hiding period changes, in the append-only `SpaceHistory` table (who, when, old and new value), but nothing reads it: the only way to see it is the database. Admins should be able to see a Space's history, and agents should be able to read it like work item history. Decide whether this joins ticket 31's "changes since T" or stays a separate list.

**Blocked by:** 11

**Status:** done

- [x] `GET /api/spaces/{id}/history` lists a Space's entries newest first, with the account's name and kind, the key's label and the actor label, as `/work-items/{id}/history` does
- [x] The Admin page's Spaces tab shows it
- [x] Agents can read it over MCP (read-only)
  - `list_space_history`. The Postgres integration test (`SpaceTests.ASpacesHistory_ListsItsChangesNewestFirst_OverRestAndMcp`, REST and MCP) compiles but wasn't run on the machine that built this, which has no Docker. Run it before merging.

## Comments

**2026-11-12, decided while building it:**

- It stays a separate list, not part of ticket 31's "changes since T". A Space is created once and almost never renamed, so it changes a handful of times in its life; agents asking what changed want work items, and admin configuration in that feed would be noise. For the same reason the list is unpaged.
- Anyone signed in can read it, as anyone can list Spaces. The entries keep key and actor label for parity with work item history, though only human admins (who have no keys) can change a Space today.
- Changes to a Space's work items, including moves between Spaces, stay in each item's own history.
