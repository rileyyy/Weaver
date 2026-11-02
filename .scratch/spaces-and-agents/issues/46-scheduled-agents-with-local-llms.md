# 46: Scheduled and autonomous agents, including local LLMs

**What to build:** Agents run without a human starting them, on a schedule or in response to changes. For example, a local model on the homelab resolves new When phrases into dates and blocked-by links each night, or a triage agent flags dependency conflicts. The agents live outside Weaver and use their own agent accounts and keys; Weaver provides the MCP surface and the audit trail.

**Blocked by:** 07, 28, 31

**Status:** needs-triage

To decide before this is ready:
- What runs the schedule (homelab cron or Komodo job vs a Weaver-side trigger) and whether Weaver needs any webhook or subscription beyond "changes since T"
- How far an autonomous agent may act without approval: propose (comment) vs set dates and links directly
- Whether small local models need a narrower tool set or simpler instructions than Claude
- How a human reviews what an autonomous run did (history filtered by agent account or actor label)

- [ ] Decisions recorded, here or as an ADR
- [ ] One scheduled agent (e.g. nightly When resolution with a local model) runs against Weaver, verified on the homelab
