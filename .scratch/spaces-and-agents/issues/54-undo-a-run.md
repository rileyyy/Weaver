# 54: Undo an agent run

**What to build:** From the run reports page (ticket 50), an admin can undo everything one run did. Runs mostly add things (dates on undated items, links, comments), so undoing one mostly means removing those additions, using the old values in history.

**Blocked by:** 50

**Status:** needs-triage

To decide before this is ready:

- What happens when a human changed an item after the run: skip that item, or ask
- Whether undoing removes the run's comments, or only its dates and links
- Whether the undo is itself recorded as a run in history

- [ ] Rules agreed and written into this ticket
