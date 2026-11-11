# 32: MCP server instructions and the reference resource

**What to build:** The MCP server sends short instructions (under about 500 tokens) telling an agent what Weaver is and how to work in it: Spaces, Types, the When workflow, patch semantics and `expectedVersion`, the actor label, and when to ask a human (conflicts, ambiguous When phrases). Deeper documentation is an MCP resource agents read on demand. Written last so it describes the final tool set.

**Blocked by:** 07, 19, 20, 27, 28, 29, 30, 31

**Status:** done

- [x] Server instructions are under about 500 tokens, checked by a test
- [x] A reference resource covers the domain vocabulary (from the glossary), each tool's purpose, and common workflows
- [x] Every tool description is accurate for the final tool set; stale tools removed
- [ ] Verified by hand: a fresh Claude Code session can find an item by `#Number`, resolve a When, and report conflicts using only the instructions
  - Not done: needs a running server and a fresh Claude Code session. Run it after deploying.

## Comments

**2026-11-11, on closing:**

- The instructions (`backend/src/Weaver.Api/Mcp/ServerInstructions.md`) and the reference (`Mcp/Reference.md`, served as `weaver://reference`) are embedded Markdown files, so they read and diff as documents. `AgentGuidanceTests` checks the token budget with a deliberately pessimistic count (every word piece and symbol is a token), that both name only real tools, and that the reference lists every tool. A new tool now fails that test until it's added to `Reference.md`.
- Tool audit: no tool was stale. `create_work_item` now says what it can't set and that it returns the version; `update_work_item_details` says "Type". `list_all_work_items` overlaps `search_work_items` (it pages by offset and orders by rank) but was kept on purpose in ticket 29 for rebuilding the tree. Remove it if agents never use it.
- The MCP integration test (`Mcp_SendsTheInstructions_AndServesTheReference_EvenToAReadOnlyKey`) compiles but wasn't run on the machine that built this, which has no Docker. Run it before merging.
- The vocabulary in `Reference.md` is copied from `GLOSSARY.md`, and a test only checks a fixed list of 22 terms, so a new glossary term won't fail anything. Add it to the reference by hand. UI-only terms (Breadcrumb, Quick pick, Agenda) are left out on purpose.
