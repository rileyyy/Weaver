# 20: Trash: purge and retention

**What to build:** An admin sets the Trash retention period on the Admin page, and a background job purges items once they've been in the Trash longer than that. Humans can also purge items by hand. Agents can't purge and can't change retention. Purge is when an item's links, comments and attachments are finally removed. See ADR-0007.

**Blocked by:** 01, 19

**Status:** ready-for-agent

- [ ] Retention is a server setting (default stated in the deployment reference), editable by admins only
- [ ] A background job purges expired items and their subtrees, including their links and comments (attachment files join this in ticket 38)
- [ ] Humans can purge one item, or empty the Trash, from the Trash page, with confirmation
- [ ] Agents get forbidden on purge over REST and MCP
- [ ] Purges are recorded in history
- [ ] Tests cover expiry, manual purge, and agent rejection
