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
