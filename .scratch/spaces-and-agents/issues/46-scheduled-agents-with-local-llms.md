# 46: The agent runner, with nightly When resolution by a local model

**What to build:** An optional `Weaver.Runner` image that does agent work on a schedule, without a human starting it. Its first task kind resolves unresolved When phrases into dates or blocked-by links using a local model. Weaver provides the REST/MCP surface and the audit trail and never calls a model itself. See ADR-0010.

**Blocked by:** 07, 28, 31

**Status:** ready-for-human

- [x] A `Weaver.Runner` .NET worker project in the backend solution, built by CI and published as an image next to the API's
  - `backend/src/Weaver.Runner`, `docker/runner/Dockerfile`, and a `publish-runner` job pushing `ghcr.io/<owner>/weaver-runner`. The Dockerfile wasn't built locally (no Docker on the implementing machine); `dotnet publish` with the same flags was, so CI's PR build is the Dockerfile's first run.
- [x] Each task kind is configured in the runner (environment or a config file): OpenAI-compatible endpoint, model, Weaver URL, agent API key and a cron expression. Weaver stores none of it
  - `Tasks:WhenResolution:*` in `/config/runner.json` or `Tasks__WhenResolution__*` in the environment, plus optional `TimeZone`, `ModelApiKey`, `JsonMode` and `ModelTimeoutSeconds` (README, "The agent runner"). Every config problem is reported at once, exit code 2.
- [x] The runner can run one task kind once on demand (e.g. `--run when-resolution`), for testing and for outside schedulers
- [x] The When task lists unresolved items over REST, leaving out Done items with `statusCategories`, and asks the model about one item at a time (see "Small models" below)
  - REST had no unresolved-When filter (only MCP's `list_unresolved_when_items`), so `GET /api/work-items/search` gained `unresolvedWhenOnly`, which the query already had. Its Postgres test `Search_UnresolvedWhenOnly_ListsUnresolvedItems_WithFullDetail` hasn't run yet (no Docker locally).
- [x] The runner checks every answer before writing: dates parse, aren't in the past, end isn't before start, and a `blockedBy` id is one the runner offered. Anything that fails is skipped, never partly written
  - The model names a blocker by its Number (`"blockedBy": 41`), not its id: small models copy a short number back far more reliably. "Not in the past" is against the server's today. Confidence must be low, medium or high.
- [x] Writes carry an actor label naming the task and the model's stated confidence
  - `Runner when-resolution, confidence high`. The model is named by the agent account, per the Decisions.
- [x] The runner only sets dates on items that have none and only adds links; it never changes or removes existing values
  - Dates are written with the `expectedVersion` read at the start of the run and only the dates the answer gave; items already linked to the item aren't offered as blockers.
- [x] If the model endpoint can't be reached, the run stops and the process exits non-zero. A bad answer for one item is logged and skipped, and the run carries on
  - Exit code 1, also in scheduled mode (the process ends; a restart policy waits for the next time). Weaver being unreachable, refusing the key or a server error (from either) stops the run the same way. A request turned down for one item alone (Weaver 400/404/409 on its reads or writes; the model 400/413/422, e.g. a prompt too long for the context window) skips that item and is counted as failed in the run's summary.
- [x] Unit tests cover answer checking, skip handling and the "never overwrite" rule, against a fake model and a fake Weaver client
- [ ] One nightly When resolution run with a local model works against Weaver, verified on the homelab
  - Needs a human on the homelab: a runner container with an Ollama (or similar) endpoint, an agent account and read-write key. Checked so far only against a stub Weaver and stub model, which drove one blocker link and one date write through the real process.

## Decisions

Settled in a grilling session on 2026-11-11.

- **What runs the schedule:** the runner's own cron per task kind. Any outside trigger (Komodo, plain cron) can call the run-once command instead. Weaver itself adds no scheduling, webhooks or subscriptions.
- **Permissions:** an agent's permissions are its agent account plus the scope of its API key, as built in ticket 07. Nothing new.
- **How far an autonomous agent may act:** it resolves and adds (dates on undated items, links, comments, sub-items) but never changes or removes what exists, and never edits another account's comments. The one exception, replacing dates after a human edited the When, is ticket 49.
- **Small models:** the runner drives the run and the model never calls tools. Claude and other large models keep the full MCP surface, and Weaver grows no "lite" tool set.
- **Model choice:** one agent account per model and task kind (e.g. "Runner: llama3.1-8b / When"), so history and the run reports (ticket 50) compare models.
- **Reviewing what a run did:** ticket 50 (run id and the admin run reports page). Ticket 46 doesn't wait for it; the runner sends a run id once it exists.
- **Blockers by Number:** the runner offers candidate blockers and the model answers with their Number (`#41`), not their id, and the answer is still accepted only if that Number is one of the offered candidates. Small models copy a short number back far more reliably than a GUID, and the Number is what the item's title is shown with, so the model doesn't have to keep two names for one item.
- **A stopped run ends the scheduled process:** deliberate while only one task kind exists, so a missed night shows as an exited container. Revisit when a second task kind is added, since one kind's failure would then stop the others.

### Small models: how the runner asks

For each unresolved item the runner builds one prompt containing:

- the item's title, When phrase, Space and parent,
- today's date in the server time zone and the server's quick-pick ranges (`GET /quick-picks`),
- a few candidate blockers from a search on the phrase (e.g. "after drywall" finds the drywall item), each with an id and title.

It asks for strict JSON only, e.g.

```json
{"start":"2026-12-01","end":"2026-12-06","blockedBy":null,"confidence":"high"}
```

or `{"skip":"ambiguous"}`, and checks the answer before writing.

## Comments

**2026-11-02, from ticket 07 (key scopes and agent admin restrictions):**

- A propose-only agent can run on a read-only key if proposing means reading and reporting outside Weaver; adding a comment is a write, so a commenting agent needs a read-write key. A narrower "comment-only" scope doesn't exist; it would be a new `ApiKeyScope` value and a change to `Caller.EnsureCanWrite`.
- MCP decides reads from writes by each tool's `ReadOnly` annotation. Any new read tool for these agents (e.g. "changes since T") must be marked `ReadOnly = true`, or read-only keys are refused it.

**2026-11-11, grilling session:**

- Split from this ticket: 49 (re-resolving an edited When), 50 (run id and run reports), 51 (`@` mentions), 52 (letting mention requests edit), 53 (shifting dependent items), 54 (undoing a run).

**2026-11-11, review:**

- A read Weaver turns down for one item (its links, the candidate searches) now skips that item instead of stopping the run, following the rule writes already used; the run-wide reads (today, quick picks, the unresolved list) still stop it on any failure.
- A prompt the model endpoint turns down (400, 413, 422) skips that item; connection failures, timeouts, 401/403, 404 (unknown model), 429 and 5xx still stop the run. Such items are counted in a new `Failed` total in the run's summary.
- New tests for `TaskRunFactory` (dispatch, per-kind client reuse, timeouts, disposal), `TaskKind` lookups, `ScheduledTasksService` (exit code and `StopApplication`) and `WhenPrompt` (parent, no candidates, candidate lines).
- `TaskRunFactory` dispatches from a map of task kind to run builder; Weaver's timeout is the named `WeaverTimeout` (not configurable, since only the model's answer time varies enough to need tuning).
- `IsoDate` holds the `yyyy-MM-dd` format and always uses the invariant culture (the run's log line and the checker's messages didn't).
- Bare "task" replaced by "task kind" in help text, locals and the README, per the glossary.
- `HttpWeaverClient.NewRequest`'s comment now gives today's reason (the client is handed in, so the key goes on each request), not ticket 50's run id.
- Confidence is the `WhenConfidence` enum; `BlockerCandidate.From` maps an item to a candidate.
- Kept as accepted: dates written before the link (a refused date write skips the link), low-confidence answers written, blockers named by Number, and a stopped run ending the scheduled process.
- Integration tests (Postgres, Docker) not run on the reviewing machine either.
