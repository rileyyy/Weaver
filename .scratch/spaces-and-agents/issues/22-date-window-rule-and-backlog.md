# 22: Date-window rule and the Backlog

**What to build:** Board and Roadmap show only items whose dates overlap the window, so someday items stop cluttering "now". A parent with no dates still appears when any child matches; a parent with its own dates shows its own Roadmap bar. Every non-Done item with no dates lives in a new **Backlog** list, where it can be given dates. See ADR-0003.

**Blocked by:** 18

**Status:** ready-for-agent

- [ ] Board and Roadmap apply the new rule through the server query (no client logic)
- [ ] The Roadmap draws undated parents as group rows without a bar, and dated parents with their own bar
- [ ] A Backlog list shows non-Done undated items for the current Space, grouped by parent
- [ ] Setting dates on a Backlog item moves it out of the Backlog
- [ ] Docs no longer describe a missing date as open-ended
