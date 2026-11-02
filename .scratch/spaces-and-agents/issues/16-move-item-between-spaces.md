# 16: Move a work item between Spaces

**What to build:** A user or read-write agent moves a work item, with its whole subtree, into another Space as one explicit operation. It maps each item's status, priority, Type and custom fields onto the target Space's configuration, showing the mapping before it's confirmed. See ADR-0001.

**Blocked by:** 14

**Status:** ready-for-agent

- [ ] The move takes a target Space and optional target parent there, plus a status and Type mapping; unmapped values fall back to stated defaults
- [ ] Custom-field values without a matching field on the target Type follow the same rule as changing Type (ticket 14)
- [ ] The whole subtree moves atomically; links (which may cross Spaces) are kept
- [ ] Recorded in history for every moved item
- [ ] Available in the detail dialog and over MCP
- [ ] Ordinary reparenting still can't cross Spaces

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- Moving an item between Spaces must record history for the item (and every descendant it moves), attributed to the caller.
