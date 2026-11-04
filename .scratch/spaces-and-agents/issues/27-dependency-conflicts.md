# 27: Dependency conflicts

**What to build:** A **dependency conflict** is a blocked item scheduled to start before its blocker ends. Weaver flags it with a badge on the card and on the Roadmap, draws blocks links as Roadmap arrows, and lets agents query conflicts so they can raise a stale date with a human. Weaver never reschedules anything. See ADR-0009.

**Blocked by:** 26

**Status:** ready-for-agent

- [ ] Conflict detection lives in the Application layer, with tests for overlapping, touching, and undated cases
- [ ] Cards and Roadmap bars show a conflict badge, with the blocker named on hover or tap
- [ ] The Roadmap draws arrows for blocks links between visible items
- [ ] An MCP tool lists current conflicts, optionally per Space
- [ ] No code path changes dates in response to a conflict

## Where to start

Decide these first, because the tests depend on them:

- **Touching dates.** `EndDate` is inclusive (see `roadmap_bar.dart`), so a blocked item that starts on the blocker's end date overlaps it by a day. A blocked item that starts the day after is "touching" and probably isn't a conflict.
- **Undated items.** If either the blocked item's `StartDate` or the blocker's `EndDate` is missing, there's nothing to compare. Treating that as "no conflict" fits ADR-0009, since Weaver only flags a date that is actually wrong.
- **What the DTO carries.** The badge has to name the blocker, which may be in another Space or filtered out of view. The arrows need every blocks link between visible items, not only the conflicting ones. One option is to send a `blockedBy` list on `WorkItemDto` (id, number, title, and whether that link conflicts), so the client computes nothing itself.

### Backend

- `backend/src/Weaver.Application/Services/`: add a new `IDependencyConflicts` / `DependencyConflicts` service next to `WorkItemBlockers.cs`, with a record for a conflict (blocked item plus blocker). Keep it a separate interface instead of widening `IWorkItemBlockers`, which only answers the yes/no question for `whenResolved`. Use one batched query over `WorkItemLinks` where `Type == Blocks`. The links' Trash filter already hides trashed blockers, so don't add a Trash check. Take an optional Space id for the MCP tool.
- `backend/src/Weaver.Application/DependencyInjection.cs`: register the service.
- `backend/tests/Weaver.Application.Tests/DependencyConflictsTests.cs`: cover overlapping, touching, same-day, undated (each side), trashed blocker, cross-Space, and the Space filter. Base it on `WorkItemBlockersTests.cs`.
- `backend/src/Weaver.Api/Contracts/WorkItemDto.cs` and `WorkItemDtoMapper.cs`: add the new field. Today the mapper only queries blockers for items with an undated When (`WhenResolutionNeedsBlockers`). Conflicts and arrows need every item in the response, so fetch the conflicts in the same batched lookup and keep it to one query per response.
- `backend/tests/Weaver.Api.Tests/`: update `WorkItemDtoMapperTests.cs`, and add a fake next to `FakeWorkItemBlockers.cs`.
- `backend/src/Weaver.Api/Mcp/DependencyConflictTools.cs` (new): add `list_dependency_conflicts` with an optional `spaceId` and `ReadOnly = true`, following `WorkItemLinkTools.cs`. `WithToolsFromAssembly` in `Program.cs` picks it up, so it doesn't need registering. Write the description so an agent knows to raise the conflict with a human, not change the dates. Tests go in `backend/tests/Weaver.Api.Tests/Mcp/DependencyConflictToolsTests.cs`, plus a case in `Weaver.Api.IntegrationTests/McpTests.cs`.

### Frontend

- `frontend/lib/shared/models/work_item.dart`: add the field in the constructor, `fromJson` and `_copy`, as the class comment describes.
- `frontend/lib/features/board/models/work_item_card.dart` and `hierarchy_item.dart`: carry the field through `fromWorkItem`.
- `frontend/lib/features/board/widgets/dependency_conflict_badge.dart` (new): show the badge with a tooltip naming the blocker(s), modelled on `work_item_detail/widgets/when_resolved_badge.dart`. "Hover or tap" means the tooltip needs `TooltipTriggerMode.tap` on touch, because a long press picks the card up (see the title tooltip in `board_card.dart`).
- `frontend/lib/features/board/widgets/board_card.dart`: place the badge. Test it in `test/features/board/widgets/board_card_test.dart`.
- `frontend/lib/features/board/views/roadmap/widgets/roadmap_row_tile.dart`: add the badge to the bar in `_buildBar`.
- Roadmap arrows:
  - Rows are a `ListView.builder` with a fixed `_rowHeight` (40). Move that constant into `roadmap_layout.dart` so an overlay can share it.
  - Put the arrow geometry in a pure function in a new `roadmap/roadmap_arrows.dart`. It takes the visible rows, the links and `roadmapBarSpan`, and returns the start and end points. That makes it testable the way `roadmap_bar_test.dart` tests the bars.
  - Paint the arrows with a `CustomPainter` (new `roadmap/widgets/roadmap_dependency_arrows.dart`) that scrolls with the list in `roadmap_view.dart`. Skip a link when either end is collapsed, filtered out or off-window.

### Watch out for

- **Stale badges.** Changing a blocker's dates changes whether the *blocked* item conflicts, but the blocked item's version and history don't change. Changes-since-T (and so `WorkItemStore`) won't refresh it. That's the same gap ticket 26 noted for `whenResolved`. Either reload the affected items or record it as a known limitation.
- **Docs.** Drop *(planned)* from **Dependency conflict** in `GLOSSARY.md` and add a `CHANGELOG.md` entry.

## Comments

**2026-11-04, from ticket 26 (typed links):**

- A blocks link is stored once as `WorkItemLink { WorkItemId blocks LinkedWorkItemId, Type = Blocks }`; `RelationFrom(id)` reads it from either side. `IWorkItemBlockers` (Application) already finds which items have a live blocker in one query, and the Trash filter on links hides trashed blockers, so conflict detection can start from there.
