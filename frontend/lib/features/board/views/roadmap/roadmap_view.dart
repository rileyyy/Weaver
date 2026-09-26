import 'package:flutter/material.dart';
import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_header_row.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_row.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_row_tile.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_toolbar.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';

const double leftColumnWidth = 260;

/// A Gantt-style view of the same work-item tree the Hierarchy view shows
/// (see [BoardViewModel.hierarchyRoots] for the shared filter/sort/tree
/// logic) — the left column mirrors Hierarchy's indentation and
/// collapse/expand behavior, and a horizontal timeline to the right shows
/// each item's scheduled start/end as a bar. [RoadmapTimeframe] and the
/// visible window are pure view state, like Hierarchy's collapse set — not
/// persisted or shared with the time-frame *filter* in the header, which is
/// a data filter, not a display zoom level.
class RoadmapView extends StatefulWidget {
  const RoadmapView({
    super.key,
    required this.viewModel,
    required this.onItemOpened,
  });

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;

  @override
  State<RoadmapView> createState() => _RoadmapViewState();
}

class _RoadmapViewState extends State<RoadmapView> {
  final Set<String> _collapsedIds = {};
  RoadmapTimeframe _timeframe = RoadmapTimeframe.month;
  DateTime _windowStart = dateOnly(DateTime.now());

  void _shiftWindow(int direction) {
    setState(() {
      _windowStart = addDays(_windowStart, direction * _timeframe.totalDays);
    });
  }

  void _goToToday() => setState(() => _windowStart = dateOnly(DateTime.now()));

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

    final rows = <RoadmapRow>[];
    void flatten(List<HierarchyNode> nodes, int depth) {
      for (final node in nodes) {
        rows.add(RoadmapRow(node: node, depth: depth));
        if (node.children.isNotEmpty && !_collapsedIds.contains(node.item.id)) {
          flatten(node.children, depth + 1);
        }
      }
    }

    flatten(viewModel.hierarchyRoots, 0);

    final windowEnd = addDays(_windowStart, _timeframe.totalDays);
    final rangeLabel = '${formatDate(_windowStart)} → ${formatDate(windowEnd)}';

    return LayoutBuilder(
      builder: (context, constraints) {
        final timelineWidth = constraints.maxWidth - leftColumnWidth < 0
            ? 0.0
            : constraints.maxWidth - leftColumnWidth;

        return Column(
          children: [
            RoadmapToolbar(
              timeframe: _timeframe,
              rangeLabel: rangeLabel,
              onTimeframeChanged: (timeframe) =>
                  setState(() => _timeframe = timeframe),
              onPrevious: () => _shiftWindow(-1),
              onNext: () => _shiftWindow(1),
              onToday: _goToToday,
            ),
            RoadmapHeaderRow(
              timeframe: _timeframe,
              windowStart: _windowStart,
              timelineWidth: timelineWidth,
            ),
            const Divider(height: 1),
            Expanded(
              child: rows.isEmpty
                  ? const Center(
                      child: Text('No work items match the current filters.'),
                    )
                  : ListView.builder(
                      itemCount: rows.length,
                      itemBuilder: (context, index) {
                        final row = rows[index];
                        return RoadmapRowTile(
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
