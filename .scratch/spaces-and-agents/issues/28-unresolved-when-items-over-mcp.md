# 28: Unresolved When items over MCP

**What to build:** Agents, whether Claude or a local model, can list the work items whose When is unresolved and resolve them by setting dates and/or adding a blocked-by link (e.g. "after drywall" links to the drywall item). This is how When phrases get turned into dates without Weaver containing an LLM. See ADR-0003.

**Blocked by:** 25, 26

**Status:** ready-for-agent

- [ ] An MCP tool lists unresolved When items with their phrase, Space and parent, as lean summaries
- [ ] Resolving uses the existing schedule and link writes; the item then counts as resolved
- [ ] The tool's description tells agents to search for referenced items before linking, and to leave ambiguous phrases for a human
- [ ] Tests cover listing, and that resolving removes the item from the list
