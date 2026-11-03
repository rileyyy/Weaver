# 37: Stop rebuilding the whole board on every notify (F-M7)

**What to build:** Changing one card rebuilds only what depends on it, not the whole board. This is the last open code-review finding, F-M7.

**Blocked by:** 36

**Status:** done

- [x] Layouts listen to narrow slices of the shared item store (per item, per lane or per cell) instead of one notifier
- [x] A single card edit doesn't rebuild unrelated lanes, verified with a widget test that counts builds
- [x] F-M7 is ticked in the code-review findings

## Comments

**2026-11-03, on closing:**

- Narrowed per lane, not per cell: a lane's height depends on its busiest cell, so a change in one cell can resize the whole lane anyway.
- Hierarchy and Roadmap still rebuild whole on an item edit, since an edit can reorder or refilter the tree. They're only built while their tab shows.
