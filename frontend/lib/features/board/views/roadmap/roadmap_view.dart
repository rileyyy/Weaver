import 'package:flutter/material.dart';
import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/flattened_tree_row.dart';
import 'package:weaver/features/board/state/flatten_tree.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_arrows.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_layout.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_dependency_arrows.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_header_row.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_row_tile.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_toolbar.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// A Gantt-style view of the items whose dates overlap the visible window
/// (see [BoardViewModel.roadmapRoots]) — the left column mirrors
/// Hierarchy's indentation and collapse/expand behavior, and a horizontal
/// timeline to the right shows each item's scheduled start/end as a bar.
/// [RoadmapTimeframe] and the visible window are view state, like
/// Hierarchy's collapse set, and aren't persisted. The window is also what
/// the Roadmap asks the server for, within the header's time-frame filter,
/// so undated (Backlog) items stay off it (ADR-0003).
class RoadmapView extends StatefulWidget {
  const RoadmapView({
    super.key,
    required this.viewModel,
    required this.onItemOpened,
    required this.onAddTagsRequested,
  });

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;
  final void Function(String workItemId, List<String> currentTags)
  onAddTagsRequested;

  @override
  State<RoadmapView> createState() => _RoadmapViewState();
}

class _RoadmapViewState extends State<RoadmapView> {
  final Set<String> _collapsedIds = {};
  RoadmapTimeframe _timeframe = RoadmapTimeframe.month;
  DateTime _windowStart = dateOnly(DateTime.now());
  final ScrollController _rowScroll = ScrollController();

  @override
  void initState() {
    super.initState();
    _askForWindow();
  }

  @override
  void dispose() {
    widget.viewModel.closeRoadmap();
    _rowScroll.dispose();
    super.dispose();
  }

  void _askForWindow() => widget.viewModel.showRoadmapWindow((
    start: _windowStart,
    end: addDays(_windowStart, _timeframe.totalDays - 1),
  ));

  void _showWindow({DateTime? start, RoadmapTimeframe? timeframe}) {
    setState(() {
      _windowStart = start ?? _windowStart;
      _timeframe = timeframe ?? _timeframe;
    });
    _askForWindow();
  }

  void _shiftWindow(int direction) => _showWindow(
    start: addDays(_windowStart, direction * _timeframe.totalDays),
  );

  void _goToToday() => _showWindow(start: dateOnly(DateTime.now()));

  @override
  Widget build(BuildContext context) {
    final viewModel = widget.viewModel;

    if (viewModel.isHierarchyLoading || viewModel.isRoadmapMatching) {
      return const Center(child: CircularProgressIndicator());
    }

    final error = viewModel.hierarchyLoadError;
    if (error != null) {
      return LoadErrorView(message: error, onRetry: viewModel.loadHierarchy);
    }

    final rows = flattenTree(
      viewModel.roadmapRoots,
      collapsedIds: _collapsedIds,
    );

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
                  _showWindow(timeframe: timeframe),
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
                      child: Text(
                        'Nothing is scheduled in this window. Undated items '
                        'are in the Backlog.',
                        textAlign: TextAlign.center,
                      ),
                    )
                  : Stack(
                      children: [
                        _buildRows(rows, timelineWidth),
                        Positioned(
                          left: leftColumnWidth,
                          top: 0,
                          bottom: 0,
                          width: timelineWidth,
                          child: IgnorePointer(
                            child: _buildArrows(context, rows, timelineWidth),
                          ),
                        ),
                      ],
                    ),
            ),
          ],
        );
      },
    );
  }

  Widget _buildRows(List<FlattenedTreeRow> rows, double timelineWidth) {
    final viewModel = widget.viewModel;
    return ListView.builder(
      controller: _rowScroll,
      itemCount: rows.length,
      itemExtent: roadmapRowHeight,
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
          onAddTagTapped: () =>
              widget.onAddTagsRequested(row.node.item.id, row.node.item.tags),
          statusColorFor: viewModel.statusColorFor,
        );
      },
    );
  }

  Widget _buildArrows(
    BuildContext context,
    List<FlattenedTreeRow> rows,
    double timelineWidth,
  ) {
    final colorScheme = Theme.of(context).colorScheme;
    return CustomPaint(
      painter: RoadmapDependencyArrowsPainter(
        arrows: roadmapArrows(
          rows: [for (final row in rows) row.node.item],
          windowStart: _windowStart,
          totalDays: _timeframe.totalDays,
          timelineWidth: timelineWidth,
          rowHeight: roadmapRowHeight,
        ),
        rowHeight: roadmapRowHeight,
        scroll: _rowScroll,
        neutralColor: colorScheme.onSurfaceVariant,
        conflictColor: colorScheme.error,
      ),
    );
  }
}
