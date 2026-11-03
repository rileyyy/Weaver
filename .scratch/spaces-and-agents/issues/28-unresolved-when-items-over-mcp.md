# 28: Unresolved When items over MCP

**What to build:** Agents, whether Claude or a local model, can list the work items whose When is unresolved and resolve them by setting dates and/or adding a blocked-by link (e.g. "after drywall" links to the drywall item). This is how When phrases get turned into dates without Weaver containing an LLM. See ADR-0003.

**Blocked by:** 25, 26

**Status:** ready-for-agent

- [ ] An MCP tool lists unresolved When items with their phrase, Space and parent, as lean summaries
- [ ] Resolving uses the existing schedule and link writes; the item then counts as resolved
- [ ] The tool's description tells agents to search for referenced items before linking, and to leave ambiguous phrases for a human
- [ ] Tests cover listing, and that resolving removes the item from the list

## Comments

**2026-11-03, from ticket 25 (When field and quick picks):**

- "Unresolved" is `When is not null` and no start or end date (`WorkItem.IsWhenResolved`, `whenResolved` on the DTO). Filter on the same rule server-side rather than re-deriving it, and extend it with blocked-by links per ticket 26's comment.
- Agents set the phrase with `update_work_item_details` (`when`) and resolve it with `reschedule_work_item`; `GET /quick-picks` has no MCP tool yet, if agents need the server's idea of "next week".
