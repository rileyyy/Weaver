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
