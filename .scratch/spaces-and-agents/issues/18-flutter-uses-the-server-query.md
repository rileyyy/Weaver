# 18: Flutter uses the server query

**What to build:** The header's search, tag filter and time-frame filter drive the server query instead of filtering in the client. The client-side filter logic is deleted, restoring the rule that business logic doesn't live in the client. See ADR-0006.

**Blocked by:** 17

**Status:** done

- [x] Board, Hierarchy and Roadmap load through the query with the header's current filters
  - The Hierarchy and Roadmap load the Space through the query's cursor, not an offset over Rank. The board still loads its lanes from `/swimlanes`, because drill-down scope isn't a query input. While a header filter is set, every layout shows only the items the query matched for the Space (plus their ancestors in the Hierarchy), so the server alone decides what matches.
- [x] The client-side filter logic and its tests are removed; equivalent cases are covered by server tests
  - `WorkItemQueryServiceTests` gained the cases the client tests had that the server lacked. Three intentional differences aren't ported; see Comments.
- [x] Search stays debounced, and a late response can't overwrite a newer one
- [x] Tag filter offers OR and AND
  - Shown as Any / All once two or more tags are selected.
- [x] Widget or view-model tests cover that filter changes produce the right query

## Comments

**2026-11-05, from the implementation:**

- **Behaviour users will notice:**
  - Search no longer matches tags: the server's text search covers title, description and When only (ticket 17). Use the tag filter instead. A later ticket can add tags to the search index if that's missed.
  - A time window hides undated items, per ADR-0003, where the client treated a missing date as open-ended.
  - Text matches whole words or word prefixes ("design" finds "designers", "sign" doesn't find "design").
- **Inverted window:** the server rejects one with a 400, so `BoardFilters` keeps swapping the bounds before it queries.
- **Re-asking:** any change to an item while a filter is set asks the query again, since an edit can change what matches. It's coalesced per burst of changes, but it does re-page the whole matching set. Saved Views (ticket 23) may want the board to load through the query directly instead.
- **Before the first answer:** a new filter keeps showing the previous matches (everything, the first time) until its answer arrives. If that fails, the board says so and keeps the previous matches.
- **Order:** the query pages in Number order, so the client re-sorts the Hierarchy/Roadmap load by Rank to keep manual order. `GET /api/work-items/all` is no longer used by Flutter and can be removed.
