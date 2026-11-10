import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/flattened_tree_row.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_bar.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_layout.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_grid.dart';
import 'package:weaver/features/board/widgets/dependency_conflict_badge.dart';
import 'package:weaver/features/board/widgets/tag_badge_row.dart';

/// One Roadmap row: the item's title in the tree on the left, its bar on
/// the timeline. A parent with no dates of its own is on the Roadmap only
/// for the items under it (ADR-0003), so it's drawn as a group row: a bold
/// title and no bar.
class RoadmapRowTile extends StatelessWidget {
  static const Key barKey = ValueKey('roadmap-bar');
  static const String groupRowTooltip =
      'No dates of its own. Shown for the items under it.';
  static const double _indentPerLevel = 24;
  static const double _caretColumnWidth = 24;
  static const double _barHeight = 20;
  static const double _minBarWidth = 4;
  static const double _badgeSize = 14;

  const RoadmapRowTile({
    super.key,
    required this.row,
    required this.timeframe,
    required this.windowStart,
    required this.timelineWidth,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.onAddTagTapped,
    required this.statusColorFor,
  });

  final FlattenedTreeRow row;
  final RoadmapTimeframe timeframe;
  final DateTime windowStart;
  final double timelineWidth;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final VoidCallback onAddTagTapped;
  final Color? Function(String statusId) statusColorFor;

  @override
  Widget build(BuildContext context) {
    final item = row.node.item;
    final hasChildren = row.node.children.isNotEmpty;
    final outlineColor = Theme.of(
      context,
    ).colorScheme.outlineVariant.withValues(alpha: 0.5);
    final totalDays = timeframe.totalDays;
    final gridOffsets = weeklyGridLineOffsets(timelineWidth, totalDays);
    final todayOffset = todayLineOffset(
      windowStart: windowStart,
      timelineWidth: timelineWidth,
      totalDays: totalDays,
    );
    final bar = roadmapBarExtent(
      windowStart: windowStart,
      totalDays: totalDays,
      timelineWidth: timelineWidth,
      start: item.startDate,
      end: item.endDate,
    );
    // The badge normally rides on the bar; without one in view it moves to
    // the title so a conflict is never hidden by the window.
    final isGroupRow =
        hasChildren && item.startDate == null && item.endDate == null;
    final showTitleBadge =
        bar == null && item.blockedBy.any((blocker) => blocker.conflicts);

    return InkWell(
      onTap: onTap,
      child: SizedBox(
        height: roadmapRowHeight,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: leftColumnWidth,
              child: Padding(
                // The caret and title indent together by depth, like the
                // Hierarchy view's Title column.
                padding: EdgeInsets.only(
                  left: row.depth * _indentPerLevel,
                  right: 8,
                ),
                child: Row(
                  children: [
                    SizedBox(
                      width: _caretColumnWidth,
                      height: _caretColumnWidth,
                      child: hasChildren
                          ? IconButton(
                              padding: EdgeInsets.zero,
                              iconSize: 18,
                              constraints: const BoxConstraints(),
                              visualDensity: VisualDensity.compact,
                              icon: Icon(
                                isCollapsed
                                    ? Icons.chevron_right
                                    : Icons.expand_more,
                              ),
                              tooltip: isCollapsed ? 'Expand' : 'Collapse',
                              onPressed: onToggleCollapsed,
                            )
                          : null,
                    ),
                    const SizedBox(width: 4),
                    if (showTitleBadge) ...[
                      DependencyConflictBadge(
                        blockers: item.blockedBy,
                        size: _badgeSize,
                      ),
                      const SizedBox(width: 4),
                    ],
                    Expanded(flex: 3, child: _title(item, isGroupRow)),
                    const SizedBox(width: 6),
                    Expanded(
                      flex: 2,
                      child: TagBadgeRow(
                        tags: item.tags,
                        onAddTag: onAddTagTapped,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            SizedBox(
              width: timelineWidth,
              child: Stack(
                children: [
                  for (final offset in gridOffsets)
                    Positioned(
                      left: offset,
                      top: 0,
                      bottom: 0,
                      width: 1,
                      child: Container(color: outlineColor),
                    ),
                  if (todayOffset != null)
                    Positioned(
                      left: todayOffset,
                      top: 0,
                      bottom: 0,
                      width: 2,
                      child: Container(color: roadmapTodayColor),
                    ),
                  if (bar != null) ..._buildBar(context, item, bar),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _title(HierarchyItem item, bool isGroupRow) {
    if (!isGroupRow) {
      return Text(item.title, overflow: TextOverflow.ellipsis);
    }
    return Tooltip(
      message: groupRowTooltip,
      child: Text(
        item.title,
        overflow: TextOverflow.ellipsis,
        style: const TextStyle(fontWeight: FontWeight.bold),
      ),
    );
  }

  /// The item's schedule as a positioned [bar], clipped to the visible
  /// window — an open start/end is drawn as running off the window's edge
  /// rather than being treated as unscheduled — with its conflict badge.
  List<Widget> _buildBar(
    BuildContext context,
    HierarchyItem item,
    RoadmapBarExtent bar,
  ) {
    final left = bar.left;
    final width = bar.right - bar.left;

    return [
      Positioned(
        left: left,
        width: width < _minBarWidth ? _minBarWidth : width,
        top: (roadmapRowHeight - _barHeight) / 2,
        height: _barHeight,
        child: Container(
          key: barKey,
          decoration: BoxDecoration(
            color:
                statusColorFor(item.statusId) ??
                Theme.of(context).colorScheme.primary,
            borderRadius: BorderRadius.circular(4),
          ),
        ),
      ),
      Positioned(
        left: left + 2,
        top: (roadmapRowHeight - _badgeSize) / 2 - 1,
        child: DependencyConflictBadge(
          blockers: item.blockedBy,
          size: _badgeSize,
          backdrop: Theme.of(context).colorScheme.surface,
        ),
      ),
    ];
  }
}
