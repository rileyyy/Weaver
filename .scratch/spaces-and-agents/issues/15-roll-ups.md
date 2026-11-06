# 15: Roll-ups of number and currency fields

**What to build:** A number or currency field can opt in to **roll-up**. A parent then shows the sum of that field over its descendants, computed on read, next to its own value. It's shown in the detail dialog and the Hierarchy layout, and returned over MCP. See ADR-0002.

**Blocked by:** 14

**Status:** done

- [x] The roll-up flag is set per field on the Admin page
- [x] Sums cover all descendants of any Type that have the field, and ignore trashed items once the Trash exists
- [x] Currency roll-ups only sum values in the same currency, or the field fixes one currency; decide and test
- [x] The detail dialog and Hierarchy show own value and rolled-up total distinctly
- [x] MCP detail includes rolled-up totals
- [x] Only sums are supported

## Comments

**2026-11-06, on closing:**

- Currency: **a currency field fixes one currency.** Values have no currency code (ticket 14), so all of a field's values share one. A roll-up sums one field by id, so two fields (even two "Budget"s on different Types) are never added together. Recorded as an amendment to ADR-0002; tested by `RollUpTests.Sum_NeverAddsTwoCurrencyFieldsTogether_EvenWithTheSameName`.
- "Descendants of any Type that have the field" is read as: every live descendant at any depth, through items of any Type, counting those whose Type has the field. Fields are matched by id, not by name. If same-named fields on different Types should sum together, that's a new decision (it would also have to settle currency).
- A parent shows totals of its own Type's fields under each field in the detail dialog, and totals of fields only other Types have in a "Totals of sub-items" section. The Hierarchy gets a Totals column ("Budget 1000.00 · sub-items 250.00") only when the Space has a roll-up field.
- The server sums for the detail dialog (`GET /api/work-items/{id}/roll-ups`) and MCP (`get_work_item` returns `rollUps`). The Hierarchy sums the tree it already holds, so totals follow edits without a reload; the rule is written in both C# (`RollUp.Sum`) and Dart (`HierarchyRollUps`).
- Not run here: the Postgres integration tests (`RollUpTests` in `Weaver.Api.IntegrationTests`) and the `AddCustomFieldRollUp` migration, for lack of Docker. CI is their first run; check before merging. Nothing was tried by hand in a running app.
