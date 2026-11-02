# 18: Flutter uses the server query

**What to build:** The header's search, tag filter and time-frame filter drive the server query instead of filtering in the client. The client-side filter logic is deleted, restoring the rule that business logic doesn't live in the client. See ADR-0006.

**Blocked by:** 17

**Status:** ready-for-agent

- [ ] Board, Hierarchy and Roadmap load through the query with the header's current filters
- [ ] The client-side filter logic and its tests are removed; equivalent cases are covered by server tests
- [ ] Search stays debounced, and a late response can't overwrite a newer one
- [ ] Tag filter offers OR and AND
- [ ] Widget or view-model tests cover that filter changes produce the right query
