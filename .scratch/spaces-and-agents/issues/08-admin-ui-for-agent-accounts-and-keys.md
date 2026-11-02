# 08: Admin UI for agent accounts and keys

**What to build:** On the Admin page an admin can create an agent account, issue a labelled key with a scope, copy the key from a one-time reveal, see when each key was last used, and revoke keys or deactivate the account. Agent accounts are marked as agents wherever users are listed.

**Blocked by:** 06

**Status:** ready-for-agent

- [ ] Agent accounts are listed apart from, or clearly distinguished from, humans
- [ ] Creating a key shows it once, with a copy button and a warning that it won't be shown again
- [ ] Keys list their label, scope, created and last used; revoking asks for confirmation
- [ ] Assignee pickers and user lists mark agent accounts
- [ ] Widget tests cover the create-and-reveal flow and revoke
