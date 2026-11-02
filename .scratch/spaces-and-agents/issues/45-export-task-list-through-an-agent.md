# 45: Export a task list through an agent

**What to build:** An external agent (Claude, or a local model at home) can take a work item's task list, such as a shopping list, and send it somewhere useful, for example by email. Weaver itself doesn't send anything: it gives the agent the task lines through MCP, and the agent uses its own tools to deliver them.

**Blocked by:** 35

**Status:** needs-triage

To decide before this is ready:
- Whether agents just read the description's Markdown, or Weaver exposes parsed task lines (text and ticked state) as an MCP tool
- Whether the export should be recorded on the item (a comment or history entry)
- Which agent and delivery channel is the first target

- [ ] Agreed MCP shape for reading a task list
- [ ] An agent exports a shopping list end to end, verified by hand
