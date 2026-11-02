# 06: Agent accounts and API-key sign-in, with `whoami`

**What to build:** An admin can create an **agent account** (a name and description, no password) and issue it **API keys**. A key has a label and a scope (read-only or read-write), is shown once, is stored hashed, and can be revoked. An MCP client sends the key as a header and is authenticated as that agent account without ever refreshing a token. `whoami` tells an agent which account, key and scope it is using. This ticket covers REST and MCP; scope enforcement is ticket 07 and the admin UI is ticket 08. See ADR-0004.

**Blocked by:** 05

**Status:** done

- [x] Admin-only REST endpoints create, list and deactivate agent accounts, and create, list and revoke their keys
- [x] Creating a key returns the plaintext once; only a hash is stored; listing shows label, scope, created and last used, never the key
- [x] A valid key header authenticates as the agent account on both REST and MCP; revoked keys and keys of deactivated accounts get 401
- [x] Agent accounts can't sign in with a password, and human accounts can't have keys
- [x] The caller context carries the key
- [x] MCP `whoami` returns the account name and kind, the key label and the scope
- [x] Integration tests against real Postgres cover create, authenticate, revoke and deactivated account
- [x] Docs show an example `.mcp.json` that reads the key from an environment variable

## Comments

**2026-11-02, from ticket 04 (admin-only accounts):**

- Self-registration is gone; integration tests get accounts through `ApiClient.SignedInAsync` / `SignedInAdminAsync`, which sign in as the configured first admin (`FirstAdmin:*`) and create users over `POST /api/admin/users`. Agent-account tests should build on the same helpers rather than seeding the database.
- `FirstAdminBootstrapper` only runs while the Users table is empty and always creates a human. Keep agent accounts in the same table (or update its emptiness check) so a deployment with only agents doesn't get a second "first" admin.
- From ticket 05: `HttpCallerContext` always sets `ApiKeyId` to null; filling it from the key-authenticated principal is this ticket's "the caller context carries the key".
