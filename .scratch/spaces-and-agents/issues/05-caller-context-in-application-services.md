# 05: Prefactor: pass caller context into Application services

**What to build:** Every mutating Application service call knows who is calling: the account, the API key when there is one, and an optional actor label. REST controllers and MCP tools build that context from the authenticated request in one shared place. Behaviour doesn't change; this makes history (ticket 10) and key scopes (ticket 07) easy to add.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] A caller-context abstraction exists in the Application layer and is resolved once per request, not threaded by hand through each controller
- [ ] REST and MCP both populate it from the authenticated principal; key and actor label are empty for now
- [ ] Services that mutate work items, comments, links and lookups can read it
- [ ] No behaviour change: existing tests pass without edits
