# 48: Enforce in the database that a child shares its parent's Space

**What to build:** Ticket 16 moves a work item and its subtree to another Space in one save, but it only checks the item's version. If someone creates a sub-item under one of the moving items, or reparents an item under one, after the move has read the subtree and before it saves, that item stays in the old Space under a parent that is now in the new one. Creating a child and reparenting both read the parent's Space without a lock, so row locks on the move's side don't close the gap. ADR-0001 says every child shares its parent's Space; nothing in the database holds that today.

**Blocked by:** 16

**Status:** needs-triage

- [ ] The database rejects a work item whose Space differs from its parent's, however the write gets there. One option: a composite foreign key from (`ParentId`, `SpaceId`) to (`Id`, `SpaceId`), made `DEFERRABLE INITIALLY DEFERRED` so a move can change a parent and its children in one transaction. Check how it interacts with the `xmin` concurrency tokens before choosing `ON UPDATE CASCADE`
- [ ] A move that loses this race fails as a conflict (409) and changes nothing, rather than leaving a split tree
- [ ] Tested against Postgres with a sub-item created during a move
