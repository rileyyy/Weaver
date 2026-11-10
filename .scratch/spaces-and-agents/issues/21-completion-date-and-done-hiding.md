# 21: Completion date and done-hiding

**What to build:** Moving a work item into a Done-category status records a completion date; moving it out clears it. Each Space has a done-hiding period (default 14 days), set on the Admin page. The Board hides Done items completed longer ago than that, with a "show done" toggle to bring them back.

**Blocked by:** 12, 18

**Status:** done

- [x] Completion date is set on entering Done and cleared on leaving it, with domain tests; it's recorded in history
- [x] The done-hiding period is a per-Space setting, editable by admins, readable by agents
- [x] The server query can exclude Done items completed before a cutoff, and the Board uses it
- [x] A "show done" toggle on the Board reveals them
- [x] Completion date is returned over REST and MCP

## Comments

**2026-11-02, from ticket 01 (server time zone):**

- If the completion date is a calendar date, take it from `IServerCalendar.TodayAsync`, not the UTC clock, and compute the done-hiding cutoff the same way, so an item completed late in the evening doesn't land on tomorrow's date.

**2026-11-10, on implementation (`feature/completion-date-and-done-hiding`):**

- `completionDate` is a calendar date from `IServerCalendar`. Every path that sets a status or changes what it means goes through one domain rule: create, status change, deleting a status into a replacement, Space moves, occurrence copies, and an admin moving a status into or out of Done. Moving between two Done statuses keeps the original date.
- The period is `Space.DoneHidingDays` (1–3650, default 14). Admins set it with `PUT /api/spaces/{id}/done-hiding-period`, and agents read it from `list_spaces`. The query input is `applyDoneHiding`, and each item uses its own Space's period. An item completed exactly N days ago still shows.
- The migration sets existing Done items' date to their `UpdatedAtUtc` day, in the server time zone.
- Not verified here: Docker wasn't available, so `Weaver.Api.IntegrationTests` (including the Postgres done-hiding query test) and the migration's backfill haven't run against Postgres. Run both before merging.
- Follow-up: `/work-items/swimlanes` has no done-hiding input, so on first load old Done cards can show for one round trip until the Board's query answers.
