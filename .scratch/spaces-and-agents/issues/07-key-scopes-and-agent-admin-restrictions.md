# 07: Enforce key scopes and keep agents out of admin writes

**What to build:** A read-only key can read everything and write nothing. A read-write key can do what a non-admin human can, but no agent can ever change admin data: server settings, statuses, priorities, Types and fields, accounts or privileges. This holds the same way over REST and MCP. See ADR-0004.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] Every write is rejected for read-only keys with a clear forbidden error (problem+json over REST, a tool error over MCP)
- [ ] Admin writes are rejected for every agent account, whatever its scope
- [ ] Enforcement lives in one place, not repeated per controller and tool, so new endpoints are covered by default
- [ ] Tests cover read-only write rejection, read-write success, and agent admin-write rejection, over both REST and MCP
