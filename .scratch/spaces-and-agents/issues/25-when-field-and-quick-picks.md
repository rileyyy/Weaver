# 25: When field, with quick picks and free typing

**What to build:** Every work item has an optional free-text **When** ("before the Christmas market", "after drywall"). In the detail dialog you can type one, or pick a quick option (this weekend, next week, next month) that sets dates directly in the server time zone. A When counts as resolved once it's backed by dates or a blocked-by link; the item keeps the phrase alongside. Weaver has no LLM; agents resolve phrases (ticket 28). See ADR-0003.

**Blocked by:** 01, 09

**Status:** ready-for-agent

- [ ] When is stored on the item, patchable over REST and MCP, and recorded in history
- [ ] Quick picks are resolved to dates by the server, in the configured time zone, with tests around week and month boundaries
- [ ] The detail dialog shows the When text, whether it's resolved, and the quick picks
- [ ] Typing a When without dates leaves it unresolved

## Comments

**2026-11-02, from ticket 01 (server time zone):**

- Resolve quick picks (this weekend, next week, next month) on the server from `IServerCalendar.TodayAsync`, so they agree with repeating items and the Agenda; the app can't convert to the server zone itself.

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- When belongs with the descriptive fields: add `Optional<string?> When` to `WorkItemDetailsPatch` and a `WorkItemField.When`, and it's patchable over `PATCH /work-items/{id}/details` and `update_work_item_details` with no new endpoint. A quick pick that sets dates goes through `PATCH /schedule`, which already keeps a date that's left out.
