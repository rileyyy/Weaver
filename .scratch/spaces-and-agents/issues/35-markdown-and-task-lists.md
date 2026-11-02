# 35: Markdown with tickable task lists

**What to build:** Descriptions and comments render Markdown. `- [ ]` task lines in a description render as checkboxes you can tick; ticking edits the description with a version check, so two people ticking at once don't lose each other's change. This is Weaver's checklist and shopping-list feature.

**Blocked by:** 09

**Status:** done

- [x] Descriptions and comments render Markdown in view mode, with raw editing in edit mode
- [x] Links in rendered Markdown open safely; raw HTML isn't rendered
- [x] Ticking a box toggles exactly that line and saves with `expectedVersion`; on conflict the dialog reloads and tells the user
- [x] The line-toggling logic is a pure function with unit tests (nested lists, code blocks, `[x]` and `[X]`)
- [x] Ticks are recorded in history as description changes

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Ticking a box can send just `{ "description": ..., "expectedVersion": ... }` to `PATCH /work-items/{id}/details`; the title, layer and priority are left out and stay as they are. The detail view model's `_save` already reloads the item and shows a message on a 409.

**2026-11-02, implementation notes:**

- No backend change was needed. A tick is a description-only `PATCH /details`, which ticket 10 already records as a `description` change (`UpdateDetails_RecordsEachChangedFieldWithItsOldAndNewValue`).
- When two people tick at once, the second gets the conflict message and the reloaded description, with the other person's tick in it, and ticks again. There is no automatic retry, so nobody's tick is applied on top of text they haven't seen.
