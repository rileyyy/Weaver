import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_view.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_row.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

class RoadmapRowTile extends StatelessWidget {
  static const double _indentPerLevel = 24;
  static const double _caretColumnWidth = 24;
  static const double _rowHeight = 40;
  static const double _barHeight = 20;
  static const double _minBarWidth = 4;

  const RoadmapRowTile({super.key, 
    required this.row,
    required this.timeframe,
    required this.windowStart,
    required this.timelineWidth,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.statusColorFor,
  });

  final RoadmapRow row;
  final RoadmapTimeframe timeframe;
  final DateTime windowStart;
  final double timelineWidth;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final Color? Function(String statusId) statusColorFor;

  @override
  Widget build(BuildContext context) {
    final item = row.node.item;
    final hasChildren = row.node.children.isNotEmpty;
    final outlineColor = Theme.of(
      context,
    ).colorScheme.outlineVariant.withValues(alpha: 0.5);

    return InkWell(
      onTap: onTap,
      child: SizedBox(
        height: _rowHeight,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: leftColumnWidth,
              child: Padding(
                // Only the title text indents by depth — the caret's
                // position stays fixed, the same tradeoff Hierarchy's own
                // tile makes to keep columns aligned (see hierarchy_view.dart).
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
                    Expanded(
                      flex: 3,
                      child: Text(item.title, overflow: TextOverflow.ellipsis),
                    ),
                    const SizedBox(width: 6),
                    Expanded(flex: 2, child: TagBadgeRow(tags: item.tags)),
                  ],
                ),
              ),
            ),
            SizedBox(
              width: timelineWidth,
              child: Stack(
                children: [
                  Row(
                    children: [
                      for (var i = 0; i < timeframe.unitCount; i++)
                        Container(
                          width: timelineWidth / timeframe.unitCount,
                          height: double.infinity,
                          decoration: BoxDecoration(
                            border: Border(
                              left: BorderSide(color: outlineColor),
                            ),
                          ),
                        ),
                    ],
                  ),
                  ..._buildBar(context, item),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// The item's schedule as a positioned bar, clipped to the visible
  /// window — an open start/end (see [BoardViewModel.matchesTimeFilter]'s
  /// same convention) is drawn as running off the window's edge rather than
  /// being treated as unscheduled. Returns no widgets for an item with
  /// neither date set, or one whose schedule falls entirely outside the
  /// visible window.
  List<Widget> _buildBar(BuildContext context, HierarchyItem item) {
    final start = item.startDate;
    final end = item.endDate;
    if (start == null && end == null) return const [];

    final totalDays = timeframe.totalDays.toDouble();
    final rawStartDay = start == null
        ? 0.0
        : start.difference(windowStart).inDays.toDouble();
    // +1 so a single-day item (start == end) still renders a visible
    // one-day-wide bar rather than a zero-width one.
    final rawEndDay = end == null
        ? totalDays
        : end.difference(windowStart).inDays.toDouble() + 1;

    final clampedStart = rawStartDay.clamp(0.0, totalDays);
    final clampedEnd = rawEndDay.clamp(0.0, totalDays);
    if (clampedEnd <= clampedStart) return const [];

    final left = clampedStart / totalDays * timelineWidth;
    final width = (clampedEnd - clampedStart) / totalDays * timelineWidth;

    return [
      Positioned(
        left: left,
        width: width < _minBarWidth ? _minBarWidth : width,
        top: (_rowHeight - _barHeight) / 2,
        height: _barHeight,
        child: Container(
          decoration: BoxDecoration(
            color:
                statusColorFor(item.statusId) ??
                Theme.of(context).colorScheme.primary,
            borderRadius: BorderRadius.circular(4),
          ),
        ),
      ),
    ];
  }
}
