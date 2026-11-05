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

**2026-11-02, from ticket 07 (key scopes and agent admin restrictions):**

- Retention: route `SetRetentionAsync` through `IAdminAuthorizer` and agents are refused already; it now rejects every agent account, whatever its scope.
- Purge is human-only, not admin-only, so `IAdminAuthorizer` doesn't fit. Read-only keys are already refused (it's a write), but a read-write agent passes the write filters. Purge needs its own check on the caller's account `Kind` (e.g. via `ICallerContext`), in the service so REST, MCP and any tool share it. If there's an MCP purge tool at all, it must not be marked `ReadOnly`.

**2026-11-02, from ticket 10 (change history with actor):**

- Purging hard-deletes rows that history still refers to by id only (no FK), so a purge doesn't need to touch history. Decide whether purged items' history is kept (it is, by default) or pruned with them.

**2026-11-04, from ticket 19 (Trash: delete and restore):**

- Trashed rows have `TrashedAtUtc` and `TrashRootId`, which identifies the delete they went with. An EF global query filter hides them, so purge must read with `IgnoreQueryFilters()`. Comments, links and recurrences have matching filters.
- Purge per delete: rows `WHERE TrashRootId = <root>`. Expiry can use `TrashedAtUtc`.
- Both link FKs and the parent FK are `Restrict`. Purging therefore has to remove links touching the purged rows, and record a `link` removal on any surviving item, because trashing deliberately recorded nothing for survivors. It also has to deal with trashed children of other deletes whose `ParentId` points into the purged rows. `RestoreAsync` already handles a missing parent by moving the restored item up to the nearest live ancestor. Nulling those `ParentId`s before the delete works.
- Purged items fall back to `DeletedItemTrace` in changes since T, which is already tested with a simulated purge.


**2026-11-05, from ticket 38 (attachments):**

- Attachment rows cascade with their work item, but the files need deleting too. Run purge's row deletion through `IAttachmentFileCleanup.PurgeAsync(workItemIds, purgeRows)`: it finds the files (ignoring the Trash filter), runs `purgeRows`, which must delete *and commit*, and only then deletes the files. A purge that fails leaves the items whole and restorable.
- Pass every purged id (the whole subtree), not just the root. `AttachmentTests.PurgingATrashedItem_ThroughTheCleanupSeam_RemovesItsFileAndRow` simulates this; switch it to the real purge and tick ticket 38's purge criterion.
