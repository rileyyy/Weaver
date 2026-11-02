# 27: Dependency conflicts

**What to build:** A **dependency conflict** is a blocked item scheduled to start before its blocker ends. Weaver flags it with a badge on the card and on the Roadmap, draws blocks links as Roadmap arrows, and lets agents query conflicts so they can raise a stale date with a human. Weaver never reschedules anything. See ADR-0009.

**Blocked by:** 26

**Status:** ready-for-agent

- [ ] Conflict detection lives in the Application layer, with tests for overlapping, touching, and undated cases
- [ ] Cards and Roadmap bars show a conflict badge, with the blocker named on hover or tap
- [ ] The Roadmap draws arrows for blocks links between visible items
- [ ] An MCP tool lists current conflicts, optionally per Space
- [ ] No code path changes dates in response to a conflict
