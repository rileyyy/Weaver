# 07: Enforce key scopes and keep agents out of admin writes

**What to build:** A read-only key can read everything and write nothing. A read-write key can do what a non-admin human can, but no agent can ever change admin data: server settings, statuses, priorities, Types and fields, accounts or privileges. This holds the same way over REST and MCP. See ADR-0004.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] Every write is rejected for read-only keys with a clear forbidden error (problem+json over REST, a tool error over MCP)
- [ ] Admin writes are rejected for every agent account, whatever its scope
- [ ] Enforcement lives in one place, not repeated per controller and tool, so new endpoints are covered by default
- [ ] Tests cover read-only write rejection, read-write success, and agent admin-write rejection, over both REST and MCP

## Comments

**2026-11-02, from ticket 05 (caller context):**

- `ICallerContext` (Application) / `HttpCallerContext` (Api) is the natural single place to enforce scopes: one check on the caller, not per controller or tool.
- Services that don't receive the context yet: `UserAdministrationService` (accounts and privileges), `BoardService`, and ticket 01's `ServerSettingsService`. Admin writes in all three must be refused for agents, so they need it.
- The context is an optional constructor argument (`ICallerContext? caller = null`) so ticket 05 could leave existing tests untouched. Don't rely on it being non-null: make it required here or in ticket 10.
