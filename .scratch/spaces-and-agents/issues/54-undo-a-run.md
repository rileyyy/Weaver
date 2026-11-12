# 54: Undo an agent run

**What to build:** From the run reports page (ticket 50), an admin can undo everything one run did. Runs mostly add things (dates on undated items, links, comments), so undoing one mostly means removing those additions, using the old values in history.

**Blocked by:** 50

**Status:** done

To decide before this is ready:

- What happens when a human changed an item after the run: skip that item, or ask
- Whether undoing removes the run's comments, or only its dates and links
- Whether the undo is itself recorded as a run in history

- [x] Rules agreed and written into this ticket
- [x] An admin can undo a run from the Runs view; the undo says what it undid and what it left alone, and why
- [x] Dates, links and URL links a run added are removed, sub-items it created go to the Trash, and fields it changed get their old values from history
- [x] Tests cover each kind of change, a human's later change being kept, comments being kept, and repeating an undo
  - The Application tests (InMemory), API unit tests and Flutter tests pass. The Postgres integration tests (`RunUndoTests`) compile but weren't run on the machine that built this, which has no Docker. Run them before merging, since they're the only check of the undo's transaction.

## Decisions

- If a human changed the field that the undo would change, skip that field. If a human changed a different part, continue the undo.
- Undo should not remove comments, they are a useful part of public history
- The undo should be recorded into history

Made while building it, on 2026-11-12:

- **"Changed since" means anyone but the run, after the run's first change to that field.** That includes another agent, and a change made between two of the run's own. A change the run's own later write overwrote still counts, so a human's edit is never silently undone.
- **Start and end are one schedule.** If someone moved either date the run set, both are left alone, so an undo never leaves an item with only one end of a range a person chose. A date that would end up starting after its end is also left alone.
- **An item the run created goes to the Trash** unless someone else has since touched it (any history entry, including a comment or link) or it still has sub-items. A person working on it, or adding under it, has made it theirs. The run's own sub-items go first, newest first, so each is its own delete and can be restored on its own. Its comments go to the Trash with it; that's restorable, and it's reported as undone, not kept.
- **Beyond what the runner does:** any MCP client can tag writes with a run id (ticket 50), so an undo also restores items the run deleted, and puts back the title, description, When, priority, assignee and tags. Status, parent, Space, Type, custom fields, attachments and repetition are reported as not undoable rather than guessed at, since putting them back has side effects (completion dates, moved sub-items, cleared custom fields).
- **The undo is the admin's own changes in history.** It writes through the ordinary services in one transaction, so a failure leaves nothing half undone. It has no run id of its own: an admin has no key, so it would never be listed as a run, and the history already names who undid what. Nothing marks a run as undone in the run list; add that if admins need it.
- **Repeating an undo changes nothing.** The first undo's entries are later changes than the run's, so the second one reports them as changed since the run.
- **Undo is REST and Admin page only.** It's admin-only and agents can't be admins, so there's no MCP tool.
