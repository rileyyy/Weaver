# 47: Read a Space's history

**What to build:** Ticket 11 records a Space's creation and renames in the append-only `SpaceHistory` table (who, when, old and new value), but nothing reads it: the only way to see it is the database. Admins should be able to see a Space's history, and agents should be able to read it like work item history. Decide whether this joins ticket 31's "changes since T" or stays a separate list.

**Blocked by:** 11

**Status:** needs-triage

- [ ] `GET /api/spaces/{id}/history` lists a Space's entries newest first, with the account's name and kind, the key's label and the actor label, as `/work-items/{id}/history` does
- [ ] The Admin page's Spaces tab shows it
- [ ] Agents can read it over MCP (read-only)
