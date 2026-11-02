# 06: Agent accounts and API-key sign-in, with `whoami`

**What to build:** An admin can create an **agent account** (a name and description, no password) and issue it **API keys**. A key has a label and a scope (read-only or read-write), is shown once, is stored hashed, and can be revoked. An MCP client sends the key as a header and is authenticated as that agent account without ever refreshing a token. `whoami` tells an agent which account, key and scope it is using. This ticket covers REST and MCP; scope enforcement is ticket 07 and the admin UI is ticket 08. See ADR-0004.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] Admin-only REST endpoints create, list and deactivate agent accounts, and create, list and revoke their keys
- [ ] Creating a key returns the plaintext once; only a hash is stored; listing shows label, scope, created and last used, never the key
- [ ] A valid key header authenticates as the agent account on both REST and MCP; revoked keys and keys of deactivated accounts get 401
- [ ] Agent accounts can't sign in with a password, and human accounts can't have keys
- [ ] The caller context carries the key
- [ ] MCP `whoami` returns the account name and kind, the key label and the scope
- [ ] Integration tests against real Postgres cover create, authenticate, revoke and deactivated account
- [ ] Docs show an example `.mcp.json` that reads the key from an environment variable
