# 14: Custom fields on Types

**What to build:** Each Type defines **custom fields** of these kinds: text, Markdown, number, currency, date, checkbox, single select, multi-select, user and URL. Admins define them on the Admin page. A work item shows and edits its Type's fields in the detail dialog, and agents read and write them over MCP. Values are validated against the field definitions in the Application layer. See ADR-0002.

**Blocked by:** 09, 10, 13

**Status:** ready-for-agent

- [ ] Admins add, rename, reorder and remove fields on a Type, including select options
- [ ] Values are stored in a shape that can change without a schema migration, and validated on write (wrong kind, unknown option, unknown user are rejected)
- [ ] Field updates use patch semantics and `expectedVersion`, and each change is recorded in history
- [ ] Changing an item's Type has a defined, tested rule for values that don't exist on the new Type
- [ ] Removing a field definition has a defined rule for existing values
- [ ] The detail dialog renders an editor for every field kind
- [ ] MCP returns field definitions and values and can set values; agents can't change definitions

## Comments

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Patch semantics use `Optional<T>` (`Weaver.Domain`): `Unset` means "leave alone", a value (including `null`) means "set". REST reads it with `OptionalJsonConverterFactory` (registered in `Program.cs`, so an `Optional<T>` property on a request record just works, and the OpenAPI transformer describes it as the plain value). MCP tools keep typed, nullable parameters and call `context.Supplied(nameof(arg), arg)` on the `RequestContext<CallToolRequestParams>` to tell a missing argument from `null`.
- Writes return `WorkItemWriteResult` (item + changed field names). Report custom-field changes in `ChangedFields` too, with a name that can't collide with the built-in `WorkItemField` names (e.g. `fields.<key>`).
- Take the optional `expectedVersion` and check it through `WorkItemService.FindForWriteAsync`, like every other write.
