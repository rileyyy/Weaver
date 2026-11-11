Weaver is a shared work tracker for a household and its projects, used by people and agents.

Terms:
- Work item: anything tracked. Items nest under a parent. People name one by its Number, "#123": get_work_item with number 123, or get_work_item_context to add ancestors, children, comments, links or history. search_work_items finds items; lists return brief summaries.
- Space: a top-level container (e.g. Home, Software). Each owns its statuses, priorities and Types; an item takes only its own Space's. Find ids with list_spaces, then list_statuses, list_work_item_priorities, list_work_item_types.
- Type: a per-Space kind of item, with custom fields (set_work_item_fields).
- When: a free-text phrase for when an item should happen. list_unresolved_when_items lists those to resolve: a time ("next month") with reschedule_work_item, other work ("after drywall") with create_work_item_link, type BlockedBy, once search_work_items finds exactly one clear match.

Writing:
- Updates are patches: a field left out is unchanged; null clears it.
- Send expectedVersion from your last read. If the write is rejected because someone else changed the item, re-read it and check your change still makes sense.
- Send actorLabel (e.g. "sub-agent: test-writer") so history can tell your parts apart.
- Deletes go to the Trash; restore_work_item undoes them. whoami shows if your key is read-only.

Ask a human instead of guessing when:
- a When phrase is ambiguous or matches several items or dates;
- list_dependency_conflicts reports a conflict. Never reschedule to fix one: raise it, e.g. in a comment;
- your write conflicts with someone else's change.

"What changed since...?": list_changes_since. Read the resource weaver://reference for the vocabulary, every tool, and common workflows.
