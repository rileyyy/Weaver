# 13: Types replace Layers, per Space

**What to build:** Layer is renamed **Type** everywhere (domain, API, MCP, UI copy) and becomes per-Space, like statuses. A Type keeps its colour and doesn't restrict what can be nested under what. Admins manage Types per Space. Custom fields come in ticket 14. See ADR-0002.

**Blocked by:** 12

**Status:** done

- [x] No "Layer" remains in code, API, MCP or UI copy; the glossary term Type is used throughout
- [x] Types belong to a Space; an item can only take a Type from its own Space
- [x] The Admin page manages Types per Space
- [x] Type changes on an item are recorded in history
- [x] Any Type can be nested under any other, with a test

## Comments

**2026-11-02, from ticket 10 (change history with actor):**

- History stores `layerId` changes under that name, and old entries will keep it after layers become Types. Decide whether `typeId` replaces it (and map `layerId` in `HistoryValueNames` and the Flutter `WorkItemHistoryEntry._fieldLabels`) or old entries keep showing as "Layer".

**2026-11-03, on closing:**

- History: `typeId` replaces `layerId`. The rename migration rewrites old entries' field to `typeId`, so no mapping is needed in `HistoryValueNames` or the app's labels. `PerSpaceTypes` moves Type history of items that still exist onto their Space's copy; history of deleted items keeps ids that no longer resolve.
- "No Layer remains" excludes old migrations, which record what the schema was, and old changelog entries.
- A new Space starts with Project / Goal / Task, the Layers' names and colours, so existing boards look the same. Admins can replace them per Space.
- Not run here: the Postgres integration tests and the two hand-written migrations (no Docker on this machine). Run them before merging.
