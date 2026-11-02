# 37: Stop rebuilding the whole board on every notify (F-M7)

**What to build:** Changing one card rebuilds only what depends on it, not the whole board. This is the last open code-review finding, F-M7.

**Blocked by:** 36

**Status:** ready-for-agent

- [ ] Layouts listen to narrow slices of the shared item store (per item, per lane or per cell) instead of one notifier
- [ ] A single card edit doesn't rebuild unrelated lanes, verified with a widget test that counts builds
- [ ] F-M7 is ticked in the code-review findings
