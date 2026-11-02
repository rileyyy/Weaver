# 44: Reminders through ntfy

**What to build:** Weaver sends push reminders through the homelab's existing ntfy server, for example when a work item assigned to you starts today or is overdue. ntfy is already part of the homelab stack, so no other push mechanism is needed. History (ADR-0005) is the intended event source, so there's no second event system.

**Blocked by:** 01, 10

**Status:** needs-triage

To decide before this is ready:
- Which events trigger a reminder (start date, end date, assignment, mentions, dependency conflicts) and at what time of day in the server time zone
- How a user maps to an ntfy topic, and where the ntfy URL and token are configured (admin setting vs env var)
- Whether reminders are per-user opt-in

- [ ] Reminder rules agreed and written into this ticket
- [ ] Configured ntfy server receives a reminder for a qualifying item, verified on the homelab
