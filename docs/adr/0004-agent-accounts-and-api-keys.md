# 4. Agent accounts, scoped API keys and actor labels

Status: accepted (2026-11-02)

## Context

Today an agent logs in with a human's password, which has four problems:

- It has to refresh its token every 15 minutes, and MCP has no refresh path
  for it.
- Repeated logins risk locking out the human's account.
- Its actions can't be told apart from the human's.
- `User.Kind = Agent` exists, but nothing creates agent users.

Claude Code sub-agents share their parent's MCP connection, so a pool of
per-sub-agent accounts couldn't tell them apart either.

## Decision

- **Self-registration is off.** An admin creates every account, human and
  agent.
- **Each agent runtime gets its own agent account.** Examples are "Claude
  Code (desktop)", "Ollama: llama-home" and "Nightly triage". Agent accounts
  have no password; they carry a name and a description.
- **Agent accounts sign in with API keys.**
  - Each account has one or more keys.
  - A key is long-lived, labelled and revocable.
  - It's stored hashed and shown once when it's created.
  - Each key's scope is **read-only** or **read-write**.
- **Write tools accept an optional actor label**, such as "sub-agent:
  test-writer". History records the account, the key and the label (see
  [ADR-0005](0005-change-history.md)).
- **Agents can read admin data but never change it.**
  - This covers server settings (time zone, trash retention), statuses,
    priorities, Types and fields, accounts, and admin privileges.
  - Agents can delete work items and can view and restore the Trash, but
    can't purge it (see [ADR-0007](0007-trash.md)).
- **No per-Space permissions.** Every human sees and edits everything.

We rejected two alternatives:

- **Personal access tokens on a human account.** Agents would act as that
  person.
- **OAuth for MCP.** Much more to build than this deployment needs.

## Consequences

- MCP clients set the key as a header read from an environment variable, for
  example in `.mcp.json`.
- The UI marks agent accounts and shows the actor label in history.
- Turning registration off settles the "Registration is open" question for a
  deployment reachable only over Tailscale.
