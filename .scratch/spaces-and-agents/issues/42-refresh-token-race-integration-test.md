# 42: Integration test for the refresh-token double-spend race

**What to build:** An automated test proves that two concurrent refreshes with the same refresh token can't both succeed against real Postgres. It's the last Postgres-specific path that's only verified by hand.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] An integration test fires concurrent refreshes with one token and asserts exactly one succeeds and reuse is detected as designed
- [ ] The test is reliable (no timing flakiness) across repeated runs
- [ ] Architecture docs no longer call this a manual check
