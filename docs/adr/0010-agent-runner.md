# 10. An optional agent runner, outside the API, does scheduled agent work

Status: accepted (2026-11-11)

## Context

Agents should work without a human starting them. Examples are resolving
new When phrases each night, and later answering `@` mentions in comments.
Riley wants to try different models, and to use different models for
different tasks: a small local model for When resolution, a larger hosted
model for research. Weaver must not depend on one homelab's setup, for
example a Komodo job.

[ADR-0003](0003-when-and-backlog.md) says Weaver contains no LLM. Full work
items are done by Claude Code over MCP, which needs a repo and a terminal
that a web API can't host.

## Decision

- **Weaver holds the work, and a separate runner does it.** Weaver exposes
  what needs doing (unresolved When items, later pending mentions) over REST
  and MCP. The Weaver API never calls a model.
- **The runner is an optional image, `Weaver.Runner`,** built in this repo
  and published next to the API image. It's a .NET worker that calls Weaver
  over REST with an agent key, and calls models through any
  OpenAI-compatible endpoint (Ollama, LM Studio, vLLM, OpenAI, Claude's
  compatible endpoint).
- **Each task kind is configured in the runner, never in Weaver.** That
  covers the endpoint, the model, the agent key and a cron schedule. Weaver
  stores no model endpoints or provider keys.
- **One agent account per model and task kind** (e.g. "Runner: llama3.1-8b
  / When"). History and the run reports then compare models without Weaver
  knowing what a model is.
- **The runner drives the run, and the model answers one question per
  item.** For small models, the runner gathers context, asks for strict
  JSON, checks the answer and writes it. The model never calls tools.
- **Autonomous agents only resolve or add.** They may set dates on an item
  that has none, add links, comments and sub-items. They don't change or
  remove existing values, and they don't edit anyone else's comments.
  - The one exception: **an agent may replace an item's dates when a human
    edited its When after the dates were last set.** Editing a When is a
    direct request to re-resolve it. Links are never removed.
- **Any outside trigger still works.** The runner has its own schedule and
  a command to run a task once, but Claude Code or any MCP client can do the
  same work by hand.

We rejected two alternatives:

- **Weaver calling models itself:** it would reverse ADR-0003, put provider
  secrets in Weaver's database, and still couldn't host Claude Code.
- **Only an external scheduler with hand-written scripts:** every task
  needs a new script and a new job, and it ties the feature to one homelab.

## Consequences

- ADR-0003 and [ADR-0009](0009-typed-links-and-dependency-flags.md) still
  hold: Weaver itself never calls a model or moves dates.
- Swapping a model means changing the runner's config, not Weaver.
- Shifting the items that depend on a moved item is deterministic, so it's
  a Weaver feature a human confirms, not agent work.
