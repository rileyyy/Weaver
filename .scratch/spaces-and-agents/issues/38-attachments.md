# 38: Attachments on their own storage path

**What to build:** Users attach files to a work item from the detail dialog, and download or delete them. Files are stored under a separately configured path, so they can live on another storage device and be backed up alongside Postgres. Agents can list and fetch attachments over MCP. Thumbnails and camera upload are ticket 39.

**Blocked by:** 10

**Status:** ready-for-agent

- [ ] Upload, list, download and delete over REST; files live under a configured attachments path, with metadata in Postgres
- [ ] A maximum file size is configurable and enforced; filenames can't escape the attachments path
- [ ] Attachments on trashed items are kept, and their files deleted when the item is purged
- [ ] Adding and removing attachments is recorded in history
- [ ] MCP lists attachments and fetches one; `get_work_item_context` can include them
- [ ] The attachments volume and env var are in the README's deployment reference and the example compose, with a note that backups must include it
- [ ] Integration tests for upload, download, and purge removing the file

## Comments

**2026-11-02, from tickets 02 and 03 (deployment reference):**

- The README's "Deployment reference" (under Production deployment) is the one place to list the attachments env var and volume: add a row to the backend environment table and the volumes table, plus the example compose's `.env` table if `compose.yaml` gets its own variable.
- There's no separate `migrate` service any more; the backend migrates on startup, so the attachments metadata migration needs nothing extra in compose.
- The example `backup` service only dumps Postgres and its healthcheck only watches those dumps. Attachments aren't covered by it; the README's Backups section should say so, alongside ADR-0008's "it must also back up the attachments path".

**2026-11-02, from ticket 10 (change history with actor):**

- Adding or removing an attachment changes a work item, so it must record history (e.g. an `attachment` name in `WorkItemHistoryField` with the file name as the value), and its MCP tools need the `actorLabel` argument (an integration test fails any write tool without it).
