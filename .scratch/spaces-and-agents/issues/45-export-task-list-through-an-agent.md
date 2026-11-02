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

## Comments

**2026-11-02, from ticket 35 (Markdown and task lists):**

- Task lines live only in the description's Markdown; there is no task model on the server. What counts as a task line is defined in the frontend, in `frontend/lib/shared/markdown/task_list.dart`: GitHub-flavoured `- [ ]`, `* [x]`, `+ [X]`, `1. [ ]` and `1) [ ]`, at any nesting depth or inside a block quote, with whitespace required after `]`, and nothing inside fenced or indented code blocks. If Weaver exposes parsed task lines over MCP, a backend parser has to follow the same rules; `task_list_test.dart` lists the cases to match.
- An agent that ticks items as it exports them should do what the dialog does: `update_work_item_details` with just the new description and `expectedVersion`, re-reading on a conflict, so it doesn't overwrite a tick someone made meanwhile.
