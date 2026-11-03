# 36: Board writes send `expectedVersion` through a shared item store

**What to build:** Moving, rescheduling and tagging cards from the Board, Hierarchy and Roadmap send the item's version, so a board edit can't silently overwrite someone else's change. This needs one shared per-item store in the app that every layout and the detail dialog read from, replacing per-layout copies. It closes the gap left open by code-review findings B-H3 and F-H6.

**Blocked by:** 09

**Status:** done

- [x] Items are held once in a shared store keyed by id, carrying their version; Board, Hierarchy, Roadmap and the detail dialog read from it
- [x] Every board-side write sends `expectedVersion` and updates the stored version from the response
- [x] On a conflict the item is reloaded and the user told, without disturbing other items
- [x] The post-dialog full refresh is no longer needed to keep layouts in step
- [x] `Completer`-based tests cover overlapping writes and conflicts

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Every work-item write now accepts `expectedVersion`: `PUT /status`, `/parent` and `/assignee` in the body, `PATCH /schedule` and `/tags` in the body, and `DELETE /work-items/{id}?expectedVersion=`. The board's repository already uses `PATCH` for schedule and tags but sends no version yet.
- Every write answers with `{ "item": <WorkItemDto>, "changedFields": [...] }`; `item.version` is the version to store. The board repository ignores the body today.
- `xmin` moves on any update, so a drag sent with a version is rejected if someone edited the description in the meantime. That's the trade the ticket accepts; the reload-and-tell path has to handle it gracefully, since it'll be the common conflict.

**2026-11-03, implemented in `feature/shared-item-store-with-expected-version`:**

- `WorkItemStore` (`frontend/lib/shared/data/`) holds each item once with its version. Optimistic edits sit on top of the server copy, so a failed one is dropped without undoing others. Writes to one item go one at a time, each with the version the previous answer returned, because a second write sent early would carry a stale version. A 409 reloads just that item, and the writes queued behind it fail without being sent. A load that started before a write finished doesn't put the older copy back.
- The board keeps only its layout (lanes, card order, which items the Hierarchy shows) and draws every card and row from the store. The detail dialog reads and saves its item through the store too, and a delete removes it, so no layout needs a reload after the dialog. The dialog still reports repeat-settings changes, which can create items, and the board reloads for those.
- Not checked by hand against a running server. The automated tests use fakes that reject stale versions the way the API does. Worth a manual pass: drag a card in one browser after renaming it in another, and expect the card to snap back with the "Someone else changed..." message.
- Follow-ups: cascade-deleted descendants stay in the store, unreachable but harmless. The store isn't cleared on sign-out; the next load replaces everything it shows. Ticket 37 (narrower rebuilds) can now listen per item.
