import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

const double _leftColumnWidth = 260;
const double _indentPerLevel = 24;
const double _caretColumnWidth = 24;
const double _timelineHeaderHeight = 32;
const double _rowHeight = 40;
const double _barHeight = 20;
const double _minBarWidth = 4;

const List<String> _monthAbbreviations = [
  'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
  'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
];

/// A Gantt-style timeline: how far zoomed out the timeline is, expressed as
/// how many columns it shows and how many days each column spans (for
/// [quarter]/[year], a column is a week/month rather than a single day, so
/// the whole window stays a manageable number of columns wide).
enum RoadmapTimeframe {
  week,
  fortnight,
  month,
  quarter,
  year;

  String get label => switch (this) {
        RoadmapTimeframe.week => 'Week',
        RoadmapTimeframe.fortnight => 'Fortnight',
        RoadmapTimeframe.month => 'Month',
        RoadmapTimeframe.quarter => 'Quarter',
        RoadmapTimeframe.year => 'Year',
      };

  int get unitCount => switch (this) {
        RoadmapTimeframe.week => 7,
        RoadmapTimeframe.fortnight => 14,
        RoadmapTimeframe.month => 30,
        RoadmapTimeframe.quarter => 13,
        RoadmapTimeframe.year => 12,
      };

  /// Calendar days per column. [quarter] and [year] use approximate
  /// week/month lengths (7 and 30 days) rather than each unit's true
  /// variable length — good enough for a zoomed-out overview, not a
  /// day-accurate calendar at that scale.
  int get daysPerUnit => switch (this) {
        RoadmapTimeframe.week || RoadmapTimeframe.fortnight || RoadmapTimeframe.month => 1,
        RoadmapTimeframe.quarter => 7,
        RoadmapTimeframe.year => 30,
      };

  int get totalDays => unitCount * daysPerUnit;

  String unitLabel(DateTime unitStart) => switch (this) {
        RoadmapTimeframe.week || RoadmapTimeframe.fortnight || RoadmapTimeframe.month =>
          '${unitStart.day}',
        RoadmapTimeframe.quarter => '${_monthAbbreviations[unitStart.month - 1]} ${unitStart.day}',
        RoadmapTimeframe.year => _monthAbbreviations[unitStart.month - 1],
      };
}

/// A Gantt-style view of the same work-item tree the Hierarchy view shows
/// (see [BoardViewModel.hierarchyRoots] for the shared filter/sort/tree
/// logic) — the left column mirrors Hierarchy's indentation and
/// collapse/expand behavior, and a horizontal timeline to the right shows
/// each item's scheduled start/end as a bar. [RoadmapTimeframe] and the
/// visible window are pure view state, like Hierarchy's collapse set — not
/// persisted or shared with the time-frame *filter* in the header, which is
/// a data filter, not a display zoom level.
class RoadmapView extends StatefulWidget {
  const RoadmapView({super.key, required this.viewModel, required this.onItemOpened});

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;

  @override
  State<RoadmapView> createState() => _RoadmapViewState();
}

class _RoadmapViewState extends State<RoadmapView> {
  final Set<String> _collapsedIds = {};
  RoadmapTimeframe _timeframe = RoadmapTimeframe.month;
  DateTime _windowStart = _startOfDay(DateTime.now());

  static DateTime _startOfDay(DateTime dt) => DateTime(dt.year, dt.month, dt.day);

  void _shiftWindow(int direction) {
    setState(() {
      _windowStart = _windowStart.add(Duration(days: direction * _timeframe.totalDays));
    });
  }

  void _goToToday() => setState(() => _windowStart = _startOfDay(DateTime.now()));

  @override
  Widget build(BuildContext context) {
    final viewModel = widget.viewModel;

    if (viewModel.isHierarchyLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    final error = viewModel.hierarchyLoadError;
    if (error != null) {
      return LoadErrorView(message: error, onRetry: viewModel.loadHierarchy);
    }

    final rows = <_RoadmapRow>[];
    void flatten(List<HierarchyNode> nodes, int depth) {
      for (final node in nodes) {
        rows.add(_RoadmapRow(node: node, depth: depth));
        if (node.children.isNotEmpty && !_collapsedIds.contains(node.item.id)) {
          flatten(node.children, depth + 1);
        }
      }
    }

    flatten(viewModel.hierarchyRoots, 0);

    final windowEnd = _windowStart.add(Duration(days: _timeframe.totalDays));
    final rangeLabel = '${formatDate(_windowStart)} → ${formatDate(windowEnd)}';

    return LayoutBuilder(
      builder: (context, constraints) {
        final timelineWidth = constraints.maxWidth - _leftColumnWidth < 0
            ? 0.0
            : constraints.maxWidth - _leftColumnWidth;

        return Column(
          children: [
            _RoadmapToolbar(
              timeframe: _timeframe,
              rangeLabel: rangeLabel,
              onTimeframeChanged: (timeframe) => setState(() => _timeframe = timeframe),
              onPrevious: () => _shiftWindow(-1),
              onNext: () => _shiftWindow(1),
              onToday: _goToToday,
            ),
            _RoadmapHeaderRow(
              timeframe: _timeframe,
              windowStart: _windowStart,
              timelineWidth: timelineWidth,
            ),
            const Divider(height: 1),
            Expanded(
              child: rows.isEmpty
                  ? const Center(child: Text('No work items match the current filters.'))
                  : ListView.builder(
                      itemCount: rows.length,
                      itemBuilder: (context, index) {
                        final row = rows[index];
                        return _RoadmapRowTile(
                          row: row,
                          timeframe: _timeframe,
                          windowStart: _windowStart,
                          timelineWidth: timelineWidth,
                          isCollapsed: _collapsedIds.contains(row.node.item.id),
                          onToggleCollapsed: () => setState(() {
                            if (!_collapsedIds.remove(row.node.item.id)) {
                              _collapsedIds.add(row.node.item.id);
                            }
                          }),
                          onTap: () => widget.onItemOpened(row.node.item.id),
                          statusColorFor: viewModel.statusColorFor,
                        );
                      },
                    ),
            ),
          ],
        );
      },
    );
  }
}

/// One flattened row: a [HierarchyNode] paired with how deep it sits in the
/// (currently expanded) tree — the same flattening [HierarchyView] does, so
/// both views' collapse/indent behavior stays visually consistent.
class _RoadmapRow {
  const _RoadmapRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}

class _RoadmapToolbar extends StatelessWidget {
  const _RoadmapToolbar({
    required this.timeframe,
    required this.rangeLabel,
    required this.onTimeframeChanged,
    required this.onPrevious,
    required this.onNext,
    required this.onToday,
  });

  final RoadmapTimeframe timeframe;
  final String rangeLabel;
  final ValueChanged<RoadmapTimeframe> onTimeframeChanged;
  final VoidCallback onPrevious;
  final VoidCallback onNext;
  final VoidCallback onToday;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 8,
        children: [
          IconButton(
            icon: const Icon(Icons.chevron_left),
            tooltip: 'Previous',
            onPressed: onPrevious,
          ),
          IconButton(
            icon: const Icon(Icons.chevron_right),
            tooltip: 'Next',
            onPressed: onNext,
          ),
          TextButton(onPressed: onToday, child: const Text('Today')),
          Text(rangeLabel, style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(width: 16),
          Text('Timeframe', style: Theme.of(context).textTheme.bodySmall),
          DropdownButton<RoadmapTimeframe>(
            value: timeframe,
            onChanged: (value) => value == null ? null : onTimeframeChanged(value),
            items: [
              for (final option in RoadmapTimeframe.values)
                DropdownMenuItem(value: option, child: Text(option.label)),
            ],
          ),
        ],
      ),
    );
  }
}

class _RoadmapHeaderRow extends StatelessWidget {
  const _RoadmapHeaderRow({
    required this.timeframe,
    required this.windowStart,
    required this.timelineWidth,
  });

  final RoadmapTimeframe timeframe;
  final DateTime windowStart;
  final double timelineWidth;

  @override
  Widget build(BuildContext context) {
    final unitWidth = timelineWidth / timeframe.unitCount;
    final outlineColor = Theme.of(context).colorScheme.outlineVariant;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: _leftColumnWidth,
          child: Text('Work Item', style: Theme.of(context).textTheme.labelLarge),
        ),
        SizedBox(
          width: timelineWidth,
          height: _timelineHeaderHeight,
          child: Row(
            children: [
              for (var i = 0; i < timeframe.unitCount; i++)
                Container(
                  width: unitWidth,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(border: Border(left: BorderSide(color: outlineColor))),
                  child: Text(
                    timeframe.unitLabel(windowStart.add(Duration(days: i * timeframe.daysPerUnit))),
                    style: Theme.of(context).textTheme.bodySmall,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}

class _RoadmapRowTile extends StatelessWidget {
  const _RoadmapRowTile({
    required this.row,
    required this.timeframe,
    required this.windowStart,
    required this.timelineWidth,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.statusColorFor,
  });

  final _RoadmapRow row;
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
    final outlineColor = Theme.of(context).colorScheme.outlineVariant.withValues(alpha: 0.5);

    return InkWell(
      onTap: onTap,
      child: SizedBox(
        height: _rowHeight,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: _leftColumnWidth,
              child: Padding(
                // Only the title text indents by depth — the caret's
                // position stays fixed, the same tradeoff Hierarchy's own
                // tile makes to keep columns aligned (see hierarchy_view.dart).
                padding: EdgeInsets.only(left: row.depth * _indentPerLevel, right: 8),
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
                              icon: Icon(isCollapsed ? Icons.chevron_right : Icons.expand_more),
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
                          decoration: BoxDecoration(border: Border(left: BorderSide(color: outlineColor))),
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
    final rawStartDay = start == null ? 0.0 : start.difference(windowStart).inDays.toDouble();
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
            color: statusColorFor(item.statusId) ?? Theme.of(context).colorScheme.primary,
            borderRadius: BorderRadius.circular(4),
          ),
        ),
      ),
    ];
  }
}
