# 14: Custom fields on Types

**What to build:** Each Type defines **custom fields** of these kinds: text, Markdown, number, currency, date, checkbox, single select, multi-select, user and URL. Admins define them on the Admin page. A work item shows and edits its Type's fields in the detail dialog, and agents read and write them over MCP. Values are validated against the field definitions in the Application layer. See ADR-0002.

**Blocked by:** 09, 10, 13

**Status:** done

- [x] Admins add, rename, reorder and remove fields on a Type, including select options
- [x] Values are stored in a shape that can change without a schema migration, and validated on write (wrong kind, unknown option, unknown user are rejected)
- [x] Field updates use patch semantics and `expectedVersion`, and each change is recorded in history
- [x] Changing an item's Type has a defined, tested rule for values that don't exist on the new Type
- [x] Removing a field definition has a defined rule for existing values
- [x] The detail dialog renders an editor for every field kind
- [x] MCP returns field definitions and values and can set values; agents can't change definitions

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Patch semantics use `Optional<T>` (`Weaver.Domain`): `Unset` means "leave alone", a value (including `null`) means "set". REST reads it with `OptionalJsonConverterFactory` (registered in `Program.cs`, so an `Optional<T>` property on a request record just works, and the OpenAPI transformer describes it as the plain value). MCP tools keep typed, nullable parameters and call `context.Supplied(nameof(arg), arg)` on the `RequestContext<CallToolRequestParams>` to tell a missing argument from `null`.
- Writes return `WorkItemWriteResult` (item + changed field names). Report custom-field changes in `ChangedFields` too, with a name that can't collide with the built-in `WorkItemField` names (e.g. `fields.<key>`).
- Take the optional `expectedVersion` and check it through `WorkItemService.FindForWriteAsync`, like every other write.

**2026-11-02, from ticket 10 (change history with actor):**

- Custom field changes must record history with old and new values. Field names are free-form strings in history (`Field`, max 50 chars), so a custom field needs a naming scheme that can't collide with the built-in `WorkItemField` and `WorkItemHistoryField` names.

**2026-11-04, on closing:**

- Storage: definitions are a `CustomFields` table per Type (options as jsonb, with ids); values are one jsonb document per item, keyed by field id. Changes go by `fields.<fieldId>` in `changedFields` and history; history views add `fieldName`.
- Type-change rule: changing an item's Type (a detail edit, or an admin deleting the Type with a replacement) clears all its values, each recorded in history with its old value. Carrying a value to a same-named field of the new Type was left out as a guess.
- Field-removal rule: removing a field removes its value from every item of the Type, and removing a select option removes that choice; each is recorded in the item's history, attributed to the admin.
- A field's kind is fixed once created. A user field also refuses a deactivated user, as the assignee does. Currency is an amount with at most 2 decimals and no currency code; add one when it's needed.
- Not run here: `CustomFieldTests` in the Postgres integration tests (jsonb round trip, history, both rules, MCP) and the `CustomFieldsOnTypes` migration, for lack of Docker. CI runs them; check before merging. The editors were not tried by hand in a running app.
