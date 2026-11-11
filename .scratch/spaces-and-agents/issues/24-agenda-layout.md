# 24: Agenda layout

**What to build:** **Agenda** is a list layout of dated work, grouped by day, suited to phones. It's available to any View, including Overview, and is the natural phone default.

**Blocked by:** 23

**Status:** done

- [x] Items are grouped under Overdue, Today, and following days, using the server time zone for "today"
- [x] Multi-day items appear on each day they span, or once with their range; decide and test
- [x] Tapping an item opens its detail; status can be changed inline
- [x] Works well at phone width; widget tests for grouping

## Comments

**2026-11-02, from ticket 01 (server time zone):**

- The server's "today" is `IServerCalendar.TodayAsync`. The app has no time-zone database, and the zone is only exposed as an IANA id (`GET /api/server-settings`, model `AdminServerSettings` in the admin feature). Prefer having the server query return the Overdue/Today/day grouping, or at least the server's current date, over pulling a tz package into Flutter.

**2026-11-11, on implementation (`feature/agenda-layout`):**

- **Today comes from the server:** `GET /api/calendar/today` returns `IServerCalendar.TodayAsync`. The app fetches it on each visit to the Agenda and on pull to refresh. It doesn't refetch while the tab stays open across midnight.
- **Multi-day items show once, with their range**, under the first day they're on from today. A week-long item would otherwise fill a phone screen. An open start or end is unbounded, as in ADR-0003, so an item with only an end date shows under Today until it's due.
- **Overdue** means not finished (no completion date) and ended before today. A finished past item is left out. The header's time range still applies, so a range whose start is after an item's end hides that item.
- **The Agenda draws from the Hierarchy's items**, so it follows the header's filters and hidden statuses the same way. A status changed from the list goes to the top of its new column, as from the detail dialog.
- **"Natural phone default":** Overview's layout can't be changed, so on a phone (narrower than the board's 760px breakpoint) Overview opens as the Agenda. Views saved with another layout still open in that layout on a phone. Riley should confirm this reading.
- **Not verified here:** the new Postgres integration test (`CalendarTests`) couldn't run locally because this machine has no Docker; CI runs it. Nobody ran the app by hand; the Flutter side is covered by unit and widget tests at phone width.
