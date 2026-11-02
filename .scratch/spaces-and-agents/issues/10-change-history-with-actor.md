# 10: Change history with actor

**What to build:** Every change to a work item appends a **History** entry: create, each field change, status, parent, links, comments and delete. An entry records when, the account, the API key for agents, the actor label if one was sent, the field, and the old and new values. MCP write tools accept an optional **actor label** (e.g. "sub-agent: test-writer"). The detail dialog gets a History tab that marks agent accounts and shows the label. The Application services write history, so REST and MCP can't diverge. See ADR-0005.

**Blocked by:** 05, 06

**Status:** ready-for-agent

- [ ] History is append-only: there is no API to edit or delete entries
- [ ] Every mutating work-item, comment and link service method records entries in the same transaction as the change
- [ ] MCP write tools take an optional actor label; REST writes may send one too
- [ ] The History tab lists entries newest first, with who (agent badge and actor label), when, and field old → new
- [ ] Tests assert history for each kind of change, and that a failed write records nothing
- [ ] Later tickets that add fields or operations (Spaces, Types, custom fields, When, Trash, attachments) must record history; note this in the glossary or architecture docs so it isn't missed

## Comments

**2026-11-02, from ticket 05 (caller context):**

- Make `ICallerContext` a required constructor argument on the services when history starts reading it. Existing tests that construct services directly will need a fake; that edit tracks this ticket's behaviour change.
- `HttpCallerContext.Current` throws `InvalidAccessTokenException` when there's no HttpContext. Writes from `RecurrenceGenerationWorker` (generated occurrences) have no request, so history needs a system caller for them.
- No test proves MCP calls populate the context. Add an integration test that writes over `/mcp` and asserts the history entry's account.
- The services still take explicit `userId` / `actorUserId` parameters alongside the context, and `CommentTools` reads the user id off `IHttpContextAccessor` by hand. Replace both with the context here so there's one source of "who".
