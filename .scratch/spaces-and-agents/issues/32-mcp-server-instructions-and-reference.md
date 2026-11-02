# 32: MCP server instructions and the reference resource

**What to build:** The MCP server sends short instructions (under about 500 tokens) telling an agent what Weaver is and how to work in it: Spaces, Types, the When workflow, patch semantics and `expectedVersion`, the actor label, and when to ask a human (conflicts, ambiguous When phrases). Deeper documentation is an MCP resource agents read on demand. Written last so it describes the final tool set.

**Blocked by:** 07, 19, 20, 27, 28, 29, 30, 31

**Status:** ready-for-agent

- [ ] Server instructions are under about 500 tokens, checked by a test
- [ ] A reference resource covers the domain vocabulary (from the glossary), each tool's purpose, and common workflows
- [ ] Every tool description is accurate for the final tool set; stale tools removed
- [ ] Verified by hand: a fresh Claude Code session can find an item by `#Number`, resolve a When, and report conflicts using only the instructions
