# 20: Trash: purge and retention

**What to build:** An admin sets the Trash retention period on the Admin page, and a background job purges items once they've been in the Trash longer than that. Humans can also purge items by hand. Agents can't purge and can't change retention. Purge is when an item's links, comments and attachments are finally removed. See ADR-0007.

**Blocked by:** 01, 19

**Status:** ready-for-agent

- [ ] Retention is a server setting (default stated in the deployment reference), editable by admins only
- [ ] A background job purges expired items and their subtrees, including their links and comments (attachment files join this in ticket 38)
- [ ] Humans can purge one item, or empty the Trash, from the Trash page, with confirmation
- [ ] Agents get forbidden on purge over REST and MCP
- [ ] Purges are recorded in history
- [ ] Tests cover expiry, manual purge, and agent rejection

## Comments

**2026-11-02, from ticket 01 (server time zone):**

- Retention fits the existing store without reshaping: `ServerSettings` is one seeded row with a typed, validated column per setting. Add a column with a default in the migration, a validating setter on the entity (with `Weaver.Domain.Tests/ServerSettingsTests` cases), `IServerSettingsService.SetRetentionAsync` behind `IAdminAuthorizer`, `PUT /api/server-settings/retention`, and a field on `ServerSettingsDto`. The Admin page's Server tab (`ServerSettingsAdminViewModel`) gets the field.
- MCP only has the read-only `get_server_settings` tool; don't add a write. A unit test asserts every tool on `ServerSettingsTools` is read-only.
- Retention is stored in the database, not an env var, so "default stated in the deployment reference" means a note in the README rather than a row in the environment tables (ticket 02's reference is env-var shaped).
- Decide whether expiry counts whole days in the server time zone (`IServerCalendar.TodayAsync`) or elapsed time since deletion; either is fine, but say which.
