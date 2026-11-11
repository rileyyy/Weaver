# 53: Shift dependent items when an item's dates move

**What to build:** When an item's dates move and other items are blocked by it, Weaver offers to shift them: "3 items depend on this. Shift them by +6 days?", with a preview and a human confirming. This is arithmetic over dates and blocked-by links, so no agent is involved. It keeps ADR-0009's "Weaver never moves dates by itself", because a person confirms.

**Blocked by:** 27

**Status:** needs-triage

To decide before this is ready:

- Whether the shift follows the whole chain (items blocked by the shifted items) or one level
- How undated or Done dependents are treated (probably left alone)
- Whether a shift that would create a new dependency conflict is flagged in the preview

- [ ] The shift is calculated in the Application layer and returned as a preview before anything changes
- [ ] The detail dialog offers the shift after a date change, showing the preview
- [ ] An MCP tool previews and applies the shift, so a chat agent can do "change the When and shift its dependents" in one request
- [ ] Every shifted item records history in the same transaction
- [ ] Tests cover chains, undated and Done dependents, and that nothing changes without confirmation
