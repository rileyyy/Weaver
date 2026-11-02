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
