# 08: Admin UI for agent accounts and keys

**What to build:** On the Admin page an admin can create an agent account, issue a labelled key with a scope, copy the key from a one-time reveal, see when each key was last used, and revoke keys or deactivate the account. Agent accounts are marked as agents wherever users are listed.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] Agent accounts are listed apart from, or clearly distinguished from, humans
- [ ] Creating a key shows it once, with a copy button and a warning that it won't be shown again
- [ ] Keys list their label, scope, created and last used; revoking asks for confirmation
- [ ] Assignee pickers and user lists mark agent accounts
- [ ] Widget tests cover the create-and-reveal flow and revoke

## Comments

**2026-11-02, from ticket 04 (admin-only accounts):**

- The app's user list (`ApiUserDirectoryRepository`) no longer refreshes every five minutes; it's cached until something clears it, and `ApiUserAdminRepository` clears it after every account change. Creating, renaming or deactivating agent accounts must clear it too, or new agents won't appear in assignee pickers until a reload.

**2026-11-02, from ticket 06 (agent accounts and API keys):**

- Endpoints, all admin-only, under `/api/admin/agents`: `GET` lists agents (`AgentAccountDto`: id, name, description, isActive, createdAtUtc); `POST {name, description}` creates one; `PUT {id}/active {isActive}` deactivates or reactivates; `GET {id}/keys` lists `ApiKeyDto` (id, label, scope, createdAtUtc, lastUsedAtUtc, revokedAtUtc); `POST {id}/keys {label, scope}` returns `IssuedApiKeyDto {apiKey, key}`; `DELETE {id}/keys/{keyId}` revokes (204). Scope is `"ReadOnly"` or `"ReadWrite"`.
- Revoked keys stay in the list with `revokedAtUtc` set, so show them as revoked rather than dropping them. `lastUsedAtUtc` is written at most about once a minute per key.
- Deactivating an agent doesn't revoke its keys; they stop working until it's reactivated. Say so next to the toggle.
- Agent names are free text (up to 50 characters, e.g. "Claude Code (desktop)"), not username-shaped, so don't apply the username validator to them. Descriptions are optional, up to 500.
- Agents also appear in `GET /api/admin/users` and `GET /api/users` with `kind: "Agent"`. The generic account actions there (rename, reset password, grant admin) still accept agents; hide them on agent rows (ticket 07 should refuse at least granting admin).

**2026-11-02, from ticket 07 (key scopes and agent admin restrictions):**

- `PUT /api/admin/users/{id}/admin` with `isAdmin: true` and `PUT /api/admin/users/{id}/password` now answer 409 for an agent. Rename (`PUT {id}/username`) still accepts agents and applies the human username rules, which most agent names fail (spaces, parentheses). If the agent rows need renaming, add it under `/api/admin/agents` with `User.CreateAgent`'s name rules and then refuse it on `/api/admin/users`.
- Explain the scopes next to the picker: a read-only key reads everything and writes nothing; a read-write key does what a non-admin person can. Neither can change admin data.
