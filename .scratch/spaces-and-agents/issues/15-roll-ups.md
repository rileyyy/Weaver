# 15: Roll-ups of number and currency fields

**What to build:** A number or currency field can opt in to **roll-up**. A parent then shows the sum of that field over its descendants, computed on read, next to its own value. It's shown in the detail dialog and the Hierarchy layout, and returned over MCP. See ADR-0002.

**Blocked by:** 14

**Status:** ready-for-agent

- [ ] The roll-up flag is set per field on the Admin page
- [ ] Sums cover all descendants of any Type that have the field, and ignore trashed items once the Trash exists
- [ ] Currency roll-ups only sum values in the same currency, or the field fixes one currency; decide and test
- [ ] The detail dialog and Hierarchy show own value and rolled-up total distinctly
- [ ] MCP detail includes rolled-up totals
- [ ] Only sums are supported
