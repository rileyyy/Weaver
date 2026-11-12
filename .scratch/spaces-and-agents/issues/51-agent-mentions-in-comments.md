# 51: Ask an agent through an @ mention in a comment

**What to build:** A comment that mentions an agent by handle (e.g. "@claude-research find three quotes for this") is a request that agent picks up on its next run, like When resolution. The runner gets a mentions task kind. Choosing the handle chooses the model. See ADR-0010.

**Blocked by:** 46

**Status:** done

- [x] Agent accounts have a unique **handle**, set in the admin UI and validated (lower case, digits and hyphens)
  - Lower-case letters and digits in words joined by single hyphens (`claude-research`, not `-x`, `x-` or `x--y`), at most 50 characters, unique (a unique index on `Users.Handle`; Postgres lets any number be null). Set when adding an agent or from the row menu's **Set handle**; blank clears it. `PUT /api/admin/agents/{id}/handle`, and `handle` on `POST /api/admin/agents`. The app checks the rule too, before a round trip.
- [x] When a comment is saved or edited, Weaver records its mentions in a `CommentMention` table. The table allows any account, so ticket 44 can reuse it for humans, but only agent mentions are acted on here
  - Keyed by comment and mentioned account. `User.Handle` lives on every account, though only the agent admin API sets it, so ticket 44 can give humans one.
- [x] The comment box autocompletes agent handles
  - Typing `@` in a new comment shows matching handles as chips under the field (an overlay would sit under the phone keyboard). Handles come from `GET /api/users`, which now returns `handle`, so only active accounts are suggested.
- [x] A mention is **pending** until the mentioned agent posts a comment on the same item after the mentioning comment. Editing the mentioning comment after a reply doesn't make it pending again
- [x] A read-only MCP tool `list_pending_mentions` (and a REST equivalent) returns the caller's own pending mentions, with the comment, its author and the item
  - REST: `GET /api/mentions/pending`. Oldest first.
- [x] Mentions in comments written by agent accounts are never pending, so agents can't trigger each other in a loop
- [x] A runner task kind answers pending mentions. It only adds (comments, links, sub-items, dates on undated items); a request that needs an edit gets a reply proposing the change. It always replies, even to say it couldn't do the request, which clears the mention
  - `mentions` (`--run mentions`, config under `Tasks:Mentions`). See the notes below for what "links" covers and what happens when a run stops.
- [x] Tests cover mention parsing on save and edit, the pending rule, agent-authored mentions being ignored, and the runner's always-reply behaviour
  - Domain, Application (InMemory), API, runner and Flutter tests pass. The Postgres integration tests in `MentionTests` (pending over REST end to end, the handle's unique index, an invalid handle as a 400, handles on `/api/users`) compile but weren't run: the machine that built this has no Docker. Run them before merging.

## Decisions

Settled in a grilling session on 2026-11-11.

- Mention a specific agent account, not a generic `@agent`, so routing and model comparison come from accounts Weaver already has. A default agent for a bare `@agent` can come later.
- Mention requests start add-only, the same as other autonomous work. Ticket 52 revisits letting them edit.

Made while building it, on 2026-11-11:

- **A mention is answered by a reply after the mention first appeared, not after the comment was written.** Each `CommentMention` row keeps its own time. An edit keeps the rows of mentions the comment already had (so editing after a reply doesn't ask again) and adds rows for new ones (so editing a mention into an old comment does ask). An edit that drops a mention removes its row.
- **Agent-authored mentions are stored, just never pending.** The table records what the text says; the pending query leaves them out.
- **Where a mention starts:** an `@` straight after a letter, digit, dot or another `@` isn't one, so an email address mentions nobody. A mention typed in capitals is matched lower-cased. The app's autocomplete uses the same rule.
- **Links means URL links.** The runner's answer can add links to web pages, not links to other work items: offering item links would need candidate items, as When resolution has. Item links can be added if mention runs show a need.
- **Sub-items start in the Space's first To Do status**, in board order.
- **Dates for an item that already has some are only proposed**, in the reply's "Not done" list, never written. This is enforced by the runner, not just asked of the model.
- **The reply lists what was done.** The model's reply comes first, then "Added:" (with each sub-item's Number) and "Not done:" (proposals and anything Weaver refused). It's shortened to fit Weaver's 4000-character comment limit, since a refused reply would leave the mention pending and every later run would be refused the same way.
- **An answer is limited to 10 sub-items and 10 links**, and is rejected whole otherwise, so one runaway answer can't flood an item.
- **When the model can't be reached the run stops without replying**, like When resolution, and the mentions stay pending for the next run. "Always replies" covers every mention the run could ask the model about: a rejected answer, a prompt the model turned down, or an item Weaver wouldn't read each get an "I couldn't answer this request" reply.
- **The runner now sends a run id** (`X-Agent-Run`, one GUID per run; for mentions, one per mention since 2026-11-12) on every write, for both task kinds. Tickets 46 and 50 each left this to the other, so neither did it; mention runs need it to show on the Runs tab. Ticket 50's unticked runner criterion is met by this branch.
- **A mention's additions are safe to repeat** (decided 2026-11-12, after review). The reply is what clears a mention, so if Weaver refuses it, or the run stops before it, the next run answers the same mention again. Posting the reply first was rejected: the reply must list what was actually added. Instead every write answering a mention (additions and reply) is sent with that mention's own run id, `mention <comment id>`, in whichever run makes it. Before asking the model, the runner reads the item's history, and the history of sub-items created since the mention, for entries with that run id made since the mention appeared, and:
  - skips a kind of addition (dates, sub-items, links) the earlier attempt already made, whole rather than item by item, because a retry asks the model again and may word its sub-items or pick its links differently;
  - lists the earlier attempt's additions under "Added:" in its reply, even if the retry's own answer fails the checks;
  - reports dates it set earlier as added, not as a proposal refused because the item now has dates.
  The run reports therefore show one run per answered mention rather than per scheduled run, which also makes ticket 54's undo work per request. Limits: a kind the earlier attempt only got part way through (the run stopped between two sub-items) isn't finished by the retry, and the retry's reply text comes from a new model answer, so it may describe additions slightly differently from the "Added:" list. A sub-item a person deleted between the attempts is no longer a child, so the retry doesn't see it and may add sub-items again.
- **The server instructions don't mention `list_pending_mentions`**: adding it went over their 500-token budget. The reference lists the tool and has an "Answer @ mentions" workflow.

## Comments

**2026-11-11, open questions from building it:**

- Ticket 46 decided a stopped run ends the scheduled process "while only one task kind exists", to revisit when a second is added. Mentions is the second. This branch leaves that behaviour as is: one kind's model being down still stops the other's schedule until the container restarts.
- If Weaver refuses the reply after the additions were made (rare: the additions succeeded on the same item), the mention stays pending and the next run adds them again. Not handled here. *Handled since: see "A mention's additions are safe to repeat" above.*
- The runner's shared REST code moved into `WeaverRest`. Ticket 49, built in parallel, may also touch `HttpWeaverClient`, so expect a merge conflict there, and in the EF model snapshot if 49 adds a migration (this branch's `AddHandlesAndCommentMentions` only adds `Users.Handle` and the `CommentMentions` table).
- Not checked against a real model: one mentions run against a live Weaver and a hosted model is worth doing before relying on it.
