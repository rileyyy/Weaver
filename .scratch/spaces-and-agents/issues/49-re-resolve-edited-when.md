# 49: Re-resolve a When after a human edits it

**What to build:** Editing the When phrase on an item that's already resolved is a request for an agent to resolve it again. Weaver keeps the old dates and marks the item **edited since resolved**, so it stays on the Board and Roadmap until new dates arrive. The runner can then replace those dates. This is the one exception to "autonomous agents only resolve or add" in ADR-0010.

**Blocked by:** 46

**Status:** done

- [x] An item is "edited since resolved" when a human's When edit in history is newer than the last change to its start or end date. The rule lives on the server, worked out from history, with tests for: never resolved, resolved then edited, edited then re-resolved, and edits by agents (which don't count)
- [x] The detail dialog shows an "awaiting re-resolution" badge on such items
- [x] `list_unresolved_when_items` and its REST equivalent take an `includeEdited` flag that adds these items, marked as edited
- [x] A runner option for the When task, `replaceDatesWhenPhraseEdited` (off by default), lets it replace dates on edited items only. It never removes links
- [x] Tests show the runner never replaces dates on an item that isn't edited since resolved

## Decisions

Settled in a grilling session on 2026-11-11.

- Weaver doesn't clear the dates when the When changes, because the item would drop into the Backlog until the next run.
- Items resolved by a blocked-by link aren't covered: Weaver can't tell which link resolved the When, and agents never remove links. A human removes the link if it's wrong.
- **Asking an agent directly needs nothing new.** In Claude Code or another MCP client, "re-resolve the When on #42 and shift its dependents" already works with the existing tools, under that agent's account. Shifting dependents is ticket 53, which adds an MCP tool for it.
- Asking an agent asynchronously, from a comment, is ticket 51.

## Comments

Implemented on `feature/re-resolve-edited-when`.

- **The rule** is `WhenEditsSinceResolved` in `Weaver.Application`: the item has a When and a
  start or end date, and a human account's `when` history entry has a higher id than every
  `startDate`/`endDate` entry. Entries are compared by id, not time, so changes saved in the same
  instant still order correctly. An item with no dates (resolved by a link alone) never qualifies.
  Tests cover never resolved, resolved then edited, edited then re-resolved, a human re-dating it
  themselves, agent edits, link-only resolution and clearing the When
  (`WhenEditedSinceResolvedTests`).
- **Marked everywhere, not only in the list:** every `WorkItemDto` and summary carries
  `whenEditedSinceResolved`, so the detail dialog reads it from the ordinary item.
- **Naming:** MCP and REST both take `includeEdited`, as the ticket says. In `WorkItemQuery` it's
  `IncludeEditedWhen`, because the query has many inputs. On its own, without
  `unresolvedWhenOnly`, it's rejected with a 400 rather than ignored.
- **The runner** lists with `includeEdited` only when `ReplaceDatesWhenPhraseEdited` is on. A
  replacement sends both dates (`ReplaceDatesAsync`), so a date the answer left out is cleared
  rather than left in a mismatched range. If the model answers an edited item with only a
  blocker, the link is added and the old dates are kept. The item then stays marked and is asked
  about again on the next run.
- **Not run locally:** the Postgres integration test
  (`WorkItemWhenTests.AHumanEditingAResolvedWhen_ListsItAsEdited_UntilItsDatesAreSetAgain`)
  needs Docker, which wasn't available. The rule is tested over InMemory, so CI is the first run
  of its SQL translation.
