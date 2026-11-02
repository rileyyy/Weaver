# 36: Board writes send `expectedVersion` through a shared item store

**What to build:** Moving, rescheduling and tagging cards from the Board, Hierarchy and Roadmap send the item's version, so a board edit can't silently overwrite someone else's change. This needs one shared per-item store in the app that every layout and the detail dialog read from, replacing per-layout copies. It closes the gap left open by code-review findings B-H3 and F-H6.

**Blocked by:** 09

**Status:** ready-for-agent

- [ ] Items are held once in a shared store keyed by id, carrying their version; Board, Hierarchy, Roadmap and the detail dialog read from it
- [ ] Every board-side write sends `expectedVersion` and updates the stored version from the response
- [ ] On a conflict the item is reloaded and the user told, without disturbing other items
- [ ] The post-dialog full refresh is no longer needed to keep layouts in step
- [ ] `Completer`-based tests cover overlapping writes and conflicts

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Every work-item write now accepts `expectedVersion`: `PUT /status`, `/parent` and `/assignee` in the body, `PATCH /schedule` and `/tags` in the body, and `DELETE /work-items/{id}?expectedVersion=`. The board's repository already uses `PATCH` for schedule and tags but sends no version yet.
- Every write answers with `{ "item": <WorkItemDto>, "changedFields": [...] }`; `item.version` is the version to store. The board repository ignores the body today.
- `xmin` moves on any update, so a drag sent with a version is rejected if someone edited the description in the meantime. That's the trade the ticket accepts; the reload-and-tell path has to handle it gracefully, since it'll be the common conflict.
