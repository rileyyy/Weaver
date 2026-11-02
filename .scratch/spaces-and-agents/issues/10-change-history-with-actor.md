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

**2026-11-02, from ticket 06 (agent accounts and API keys):**

- `Caller.ApiKeyId` is set for every request signed in with an API key and is null for access tokens.
- Revoking a key keeps its `ApiKeys` row, but hard-deleting an agent account (`DELETE /api/admin/users/{id}`) cascades its keys away. A history foreign key to `ApiKeys` needs `SetNull` or no FK, or deleting an agent with history must be refused the way deleting a commenter is.
- `whoami` (MCP) uses `ICallerIdentityService`, which loads the account and key named by the caller context; the History tab's "who" can use the same pair.

**2026-11-02, from ticket 09 (patch semantics and `expectedVersion`):**

- Every `WorkItem` write method (`UpdateDetails`, `Reschedule`, `SetTags`, `AssignTo`, `MoveToStatus`, `MoveToParent`) already returns the names of the fields it actually changed (`WorkItemField`, matching the JSON names), collected by `ChangedFields` in `Weaver.Domain`. History needs old and new values too: extending `ChangedFields` to record them where it already compares old and new is the natural place, rather than diffing in the service.
- A no-op write (every field set to its current value) changes nothing and doesn't move the version, so it shouldn't write history either.

**2026-11-02, from ticket 07 (key scopes and agent admin restrictions):**

- `Caller` gained a fourth positional member, `ApiKeyScope? ApiKeyScope = null`, after `ActorLabel`, and `HttpCallerContext` fills it from a `weaver_api_key_scope` claim. If this branch also edits `Caller` or `HttpCallerContext` (e.g. to fill `ActorLabel`), expect a small merge conflict there; keep both.
- Scope is enforced at the transport edge (`WriteAccessFilter` for REST, `WriteAccessToolFilter` for MCP), not in services, so history doesn't need to check it. A refused write never reaches a service, so it never records history.
