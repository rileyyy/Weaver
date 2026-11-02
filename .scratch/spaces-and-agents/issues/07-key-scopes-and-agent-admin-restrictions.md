# 07: Enforce key scopes and keep agents out of admin writes

**What to build:** A read-only key can read everything and write nothing. A read-write key can do what a non-admin human can, but no agent can ever change admin data: server settings, statuses, priorities, Types and fields, accounts or privileges. This holds the same way over REST and MCP. See ADR-0004.

**Blocked by:** 06

**Status:** done

- [x] Every write is rejected for read-only keys with a clear forbidden error (problem+json over REST, a tool error over MCP)
- [x] Admin writes are rejected for every agent account, whatever its scope
- [x] Enforcement lives in one place, not repeated per controller and tool, so new endpoints are covered by default
- [x] Tests cover read-only write rejection, read-write success, and agent admin-write rejection, over both REST and MCP

## Comments

**2026-11-02, from ticket 05 (caller context):**

- `ICallerContext` (Application) / `HttpCallerContext` (Api) is the natural single place to enforce scopes: one check on the caller, not per controller or tool.
- Services that don't receive the context yet: `UserAdministrationService` (accounts and privileges), `BoardService`, and ticket 01's `ServerSettingsService`. Admin writes in all three must be refused for agents, so they need it.
- The context is an optional constructor argument (`ICallerContext? caller = null`) so ticket 05 could leave existing tests untouched. Don't rely on it being non-null: make it required here or in ticket 10.

**2026-11-02, from ticket 06 (agent accounts and API keys):**

- A key-authenticated principal has only `sub` and `weaver_api_key_id` claims; `Caller.ApiKeyId` is set from the latter. The scope isn't a claim. `ApiKeyAuthenticator` already loads the `ApiKey` row on every request, so the cheapest route is to add `Scope` to `ApiKeySignIn` and a claim in `ApiKeyAuthenticationHandler`, rather than a second lookup per write.
- Agents are created with `IsAdmin = false`, but `PUT /api/admin/users/{id}/admin` still accepts an agent's id, and `IAdminAuthorizer` only checks `IsAdmin`. An admin could therefore make an agent an admin, and its key would pass every admin check. Refuse granting admin to agents, and/or have the admin check refuse `Kind = Agent`.
- The rest of `/api/admin/users` also accepts agents: rename applies human username rules to them, and reset-password stores a hash that `AuthService.LoginAsync` then ignores (agents are refused at login regardless).
- `AgentAccountService` and `CallerIdentityService` take `ICallerContext` as a required argument and read the acting account from it, with no `actorUserId` parameter.
