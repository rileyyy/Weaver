# 24: Agenda layout

**What to build:** **Agenda** is a list layout of dated work, grouped by day, suited to phones. It's available to any View, including Overview, and is the natural phone default.

**Blocked by:** 23

**Status:** ready-for-agent

- [ ] Items are grouped under Overdue, Today, and following days, using the server time zone for "today"
- [ ] Multi-day items appear on each day they span, or once with their range; decide and test
- [ ] Tapping an item opens its detail; status can be changed inline
- [ ] Works well at phone width; widget tests for grouping

## Comments

**2026-11-02, from ticket 01 (server time zone):**

- The server's "today" is `IServerCalendar.TodayAsync`. The app has no time-zone database, and the zone is only exposed as an IANA id (`GET /api/server-settings`, model `AdminServerSettings` in the admin feature). Prefer having the server query return the Overdue/Today/day grouping, or at least the server's current date, over pulling a tz package into Flutter.
