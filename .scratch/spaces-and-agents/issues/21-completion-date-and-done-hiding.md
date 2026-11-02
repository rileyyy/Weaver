# 21: Completion date and done-hiding

**What to build:** Moving a work item into a Done-category status records a completion date; moving it out clears it. Each Space has a done-hiding period (default 14 days), set on the Admin page. The Board hides Done items completed longer ago than that, with a "show done" toggle to bring them back.

**Blocked by:** 12, 18

**Status:** ready-for-agent

- [ ] Completion date is set on entering Done and cleared on leaving it, with domain tests; it's recorded in history
- [ ] The done-hiding period is a per-Space setting, editable by admins, readable by agents
- [ ] The server query can exclude Done items completed before a cutoff, and the Board uses it
- [ ] A "show done" toggle on the Board reveals them
- [ ] Completion date is returned over REST and MCP
