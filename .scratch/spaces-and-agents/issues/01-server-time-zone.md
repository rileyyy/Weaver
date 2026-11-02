# 01: Server time zone setting

**What to build:** An admin sets one server-wide time zone on the Admin page. It defines "today" everywhere the server reasons about dates, starting with repeating items, which use the UTC date today. Agents can read the setting but not change it. This ticket introduces the server-settings store that trash retention (ticket 20) reuses.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Server settings persist a time zone (IANA id), defaulting to UTC, editable only by admins over REST and on the Admin page
- [ ] An invalid time-zone id is rejected with a validation error
- [ ] Repeating-item generation computes "today" in the configured zone, with tests either side of midnight in a non-UTC zone
- [ ] Agents can read the time zone over MCP; there is no MCP write for it
