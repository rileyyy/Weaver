# 35: Markdown with tickable task lists

**What to build:** Descriptions and comments render Markdown. `- [ ]` task lines in a description render as checkboxes you can tick; ticking edits the description with a version check, so two people ticking at once don't lose each other's change. This is Weaver's checklist and shopping-list feature.

**Blocked by:** 09

**Status:** ready-for-agent

- [ ] Descriptions and comments render Markdown in view mode, with raw editing in edit mode
- [ ] Links in rendered Markdown open safely; raw HTML isn't rendered
- [ ] Ticking a box toggles exactly that line and saves with `expectedVersion`; on conflict the dialog reloads and tells the user
- [ ] The line-toggling logic is a pure function with unit tests (nested lists, code blocks, `[x]` and `[X]`)
- [ ] Ticks are recorded in history as description changes

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Ticking a box can send just `{ "description": ..., "expectedVersion": ... }` to `PATCH /work-items/{id}/details`; the title, layer and priority are left out and stay as they are. The detail view model's `_save` already reloads the item and shows a message on a 409.
