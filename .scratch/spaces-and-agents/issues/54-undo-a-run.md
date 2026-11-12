# 54: Undo an agent run

**What to build:** From the run reports page (ticket 50), an admin can undo everything one run did. Runs mostly add things (dates on undated items, links, comments), so undoing one mostly means removing those additions, using the old values in history.

**Blocked by:** 50

**Status:** ready-for-agent

To decide before this is ready:

- What happens when a human changed an item after the run: skip that item, or ask
- Whether undoing removes the run's comments, or only its dates and links
- Whether the undo is itself recorded as a run in history

- [ ] Rules agreed and written into this ticket

## Decisions

- If a human changed the field that the undo would change, skip that field. If a human changed a different part, continue the undo.
- Undo should not remove comments, they are a useful part of public history
- The undo should be recorded into history
