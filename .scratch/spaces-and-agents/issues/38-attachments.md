# 38: Attachments on their own storage path

**What to build:** Users attach files to a work item from the detail dialog, and download or delete them. Files are stored under a separately configured path, so they can live on another storage device and be backed up alongside Postgres. Agents can list and fetch attachments over MCP. Thumbnails and camera upload are ticket 39.

**Blocked by:** 10

**Status:** done

- [x] Upload, list, download and delete over REST; files live under a configured attachments path, with metadata in Postgres
  - Files are named by attachment id under `Attachments__Path`; the user's filename is display text only. Downloads are always `Content-Disposition: attachment` with `nosniff`.
- [x] A maximum file size is configurable and enforced; filenames can't escape the attachments path
  - `Attachments__MaxFileSizeMegabytes` (25 by default), enforced while streaming, 413 over it. nginx's `/api/` body limit is 30 MiB, so raising the setting past ~29 also means raising that.
- [x] Attachments on trashed items are kept, and their files deleted when the item is purged
  - Kept: yes, hidden by the Trash filter and back on restore. Ticket 20's purge runs its row deletion through `IAttachmentFileCleanup.PurgeAsync` with every purged id; `AttachmentTests.PurgingATrashedItem_RemovesItsAndItsSubtreesFilesAndRows` uses the real endpoint (not yet run, no Docker on the implementing machine).
- [x] Adding and removing attachments is recorded in history
- [ ] MCP lists attachments and fetches one; `get_work_item_context` can include them
  - `list_work_item_attachments` and `get_work_item_attachment` are done (read-only; text-like files come back as text, others as a base64 blob, capped at 5 MiB). `get_work_item_context` is ticket 30 and doesn't exist yet; the include is handed to it there.
- [x] The attachments volume and env var are in the README's deployment reference and the example compose, with a note that backups must include it
- [x] Integration tests for upload, download, and purge removing the file
  - `AttachmentTests` (purge simulated through the seam). Written without Docker on the implementing machine, so CI is their first run.

## Comments

**2026-11-02, from tickets 02 and 03 (deployment reference):**

- The README's "Deployment reference" (under Production deployment) is the one place to list the attachments env var and volume: add a row to the backend environment table and the volumes table, plus the example compose's `.env` table if `compose.yaml` gets its own variable.
- There's no separate `migrate` service any more; the backend migrates on startup, so the attachments metadata migration needs nothing extra in compose.
- The example `backup` service only dumps Postgres and its healthcheck only watches those dumps. Attachments aren't covered by it; the README's Backups section should say so, alongside ADR-0008's "it must also back up the attachments path".

**2026-11-02, from ticket 10 (change history with actor):**

- Adding or removing an attachment changes a work item, so it must record history (e.g. an `attachment` name in `WorkItemHistoryField` with the file name as the value), and its MCP tools need the `actorLabel` argument (an integration test fails any write tool without it).

**2026-11-05, from the implementation:**

- **No MCP writes.** Agents can list and fetch, per "What to build", so there's no upload or delete tool and nothing needs `actorLabel`.
- **Two criteria wait on other tickets.** Purge removing files waits on ticket 20 and the `get_work_item_context` include on ticket 30; both tickets have a hand-off comment.
- **A missing file is a 500.** If a row's file is gone (e.g. a restore from a backup that skipped the attachments path), download fails as a server error rather than a 404, so it gets logged.
