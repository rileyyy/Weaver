# 13: Types replace Layers, per Space

**What to build:** Layer is renamed **Type** everywhere (domain, API, MCP, UI copy) and becomes per-Space, like statuses. A Type keeps its colour and doesn't restrict what can be nested under what. Admins manage Types per Space. Custom fields come in ticket 14. See ADR-0002.

**Blocked by:** 12

**Status:** ready-for-agent

- [ ] No "Layer" remains in code, API, MCP or UI copy; the glossary term Type is used throughout
- [ ] Types belong to a Space; an item can only take a Type from its own Space
- [ ] The Admin page manages Types per Space
- [ ] Type changes on an item are recorded in history
- [ ] Any Type can be nested under any other, with a test
