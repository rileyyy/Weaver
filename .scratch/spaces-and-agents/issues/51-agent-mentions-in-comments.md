# 51: Ask an agent through an @ mention in a comment

**What to build:** A comment that mentions an agent by handle (e.g. "@claude-research find three quotes for this") is a request that agent picks up on its next run, like When resolution. The runner gets a mentions task kind. Choosing the handle chooses the model. See ADR-0010.

**Blocked by:** 46

**Status:** needs-triage

- [ ] Agent accounts have a unique **handle**, set in the admin UI and validated (lower case, digits and hyphens)
- [ ] When a comment is saved or edited, Weaver records its mentions in a `CommentMention` table. The table allows any account, so ticket 44 can reuse it for humans, but only agent mentions are acted on here
- [ ] The comment box autocompletes agent handles
- [ ] A mention is **pending** until the mentioned agent posts a comment on the same item after the mentioning comment. Editing the mentioning comment after a reply doesn't make it pending again
- [ ] A read-only MCP tool `list_pending_mentions` (and a REST equivalent) returns the caller's own pending mentions, with the comment, its author and the item
- [ ] Mentions in comments written by agent accounts are never pending, so agents can't trigger each other in a loop
- [ ] A runner task kind answers pending mentions. It only adds (comments, links, sub-items, dates on undated items); a request that needs an edit gets a reply proposing the change. It always replies, even to say it couldn't do the request, which clears the mention
- [ ] Tests cover mention parsing on save and edit, the pending rule, agent-authored mentions being ignored, and the runner's always-reply behaviour

## Decisions

Settled in a grilling session on 2026-11-11.

- Mention a specific agent account, not a generic `@agent`, so routing and model comparison come from accounts Weaver already has. A default agent for a bare `@agent` can come later.
- Mention requests start add-only, the same as other autonomous work. Ticket 52 revisits letting them edit.
