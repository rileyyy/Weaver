# 52: Revisit letting mention requests edit existing values

**What to build:** Decide whether an explicit `@` mention request ("@claude-research move this to next week") may change existing values, unlike other autonomous work, and if so build it. A mention is a direct human request, but it's answered with no one watching.

**Blocked by:** 50, 51

**Status:** needs-triage

To decide before this is ready:

- Whether the run reports (ticket 50) show the mention agents behaving well enough to trust with edits
- Which edits: only the mentioned item, or its sub-items too; whether status, assignee and deletes are excluded
- Whether undoing a run (ticket 54) is needed first as a safety net

- [ ] Decision recorded here, or as an amendment to ADR-0010
