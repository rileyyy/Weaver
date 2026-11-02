# 34: Reorder cards within a column

**What to build:** On the Board you can drag a card up or down within its status cell to change its order, as well as between columns. The order persists and everyone sees it.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Dropping a card above or below another in the same cell reorders it, with a drop indicator
- [ ] Order is persisted with the existing rank mechanism, including respacing on ties
- [ ] Moving between columns can also place the card at a chosen position
- [ ] Failed reorders roll back without disturbing other cards
- [ ] Widget tests for in-cell drag
