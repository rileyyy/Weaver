# 49: Re-resolve a When after a human edits it

**What to build:** Editing the When phrase on an item that's already resolved is a request for an agent to resolve it again. Weaver keeps the old dates and marks the item **edited since resolved**, so it stays on the Board and Roadmap until new dates arrive. The runner can then replace those dates. This is the one exception to "autonomous agents only resolve or add" in ADR-0010.

**Blocked by:** 46

**Status:** needs-triage

- [ ] An item is "edited since resolved" when a human's When edit in history is newer than the last change to its start or end date. The rule lives on the server, worked out from history, with tests for: never resolved, resolved then edited, edited then re-resolved, and edits by agents (which don't count)
- [ ] The detail dialog shows an "awaiting re-resolution" badge on such items
- [ ] `list_unresolved_when_items` and its REST equivalent take an `includeEdited` flag that adds these items, marked as edited
- [ ] A runner option for the When task, `replaceDatesWhenPhraseEdited` (off by default), lets it replace dates on edited items only. It never removes links
- [ ] Tests show the runner never replaces dates on an item that isn't edited since resolved

## Decisions

Settled in a grilling session on 2026-11-11.

- Weaver doesn't clear the dates when the When changes, because the item would drop into the Backlog until the next run.
- Items resolved by a blocked-by link aren't covered: Weaver can't tell which link resolved the When, and agents never remove links. A human removes the link if it's wrong.
- **Asking an agent directly needs nothing new.** In Claude Code or another MCP client, "re-resolve the When on #42 and shift its dependents" already works with the existing tools, under that agent's account. Shifting dependents is ticket 53, which adds an MCP tool for it.
- Asking an agent asynchronously, from a comment, is ticket 51.
