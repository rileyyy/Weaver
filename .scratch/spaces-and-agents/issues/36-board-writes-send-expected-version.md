# 36: Board writes send `expectedVersion` through a shared item store

**What to build:** Moving, rescheduling and tagging cards from the Board, Hierarchy and Roadmap send the item's version, so a board edit can't silently overwrite someone else's change. This needs one shared per-item store in the app that every layout and the detail dialog read from, replacing per-layout copies. It closes the gap left open by code-review findings B-H3 and F-H6.

**Blocked by:** 09

**Status:** ready-for-agent

- [ ] Items are held once in a shared store keyed by id, carrying their version; Board, Hierarchy, Roadmap and the detail dialog read from it
- [ ] Every board-side write sends `expectedVersion` and updates the stored version from the response
- [ ] On a conflict the item is reloaded and the user told, without disturbing other items
- [ ] The post-dialog full refresh is no longer needed to keep layouts in step
- [ ] `Completer`-based tests cover overlapping writes and conflicts
