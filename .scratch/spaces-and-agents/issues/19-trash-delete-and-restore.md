# 19: Trash: delete and restore

**What to build:** Deleting a work item moves it and its subtree to the **Trash** instead of destroying it. Every normal read excludes trashed items; the query's include-deleted flag is the only way to see them. Humans and read-write agents can view the Trash and restore from it, in the app and over MCP. Purging and retention are ticket 20. See ADR-0007.

**Blocked by:** 10, 17

**Status:** ready-for-agent

- [ ] Delete trashes the item and its subtree; links, comments and attachments are kept
- [ ] All reads (lists, query, roll-ups, counts, link targets) exclude trashed items unless include-deleted is set
- [ ] Restore puts the item back under its nearest ancestor that still exists, or as a root of its Space
- [ ] Repeating items: trashing or restoring a generated copy doesn't make the generator recreate or duplicate it, and trashing a template stops its schedule; decide the rules and test them
- [ ] Delete and restore are recorded in history
- [ ] A Trash page lists trashed items with who deleted them and when, with restore
- [ ] MCP can list and restore Trash; read-only keys can list only
