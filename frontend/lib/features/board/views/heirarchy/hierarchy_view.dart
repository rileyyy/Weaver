import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_column_widths.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/heirarchy_header_row.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/heirarchy_item_tile.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/heirarchy_row.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';

/// Every work item nested under its parent, respecting the same time/search/
/// status filters and sort order as the swim-lane board (see
/// [BoardViewModel.hierarchyRoots]). Loaded lazily by the board view the
/// first time this tab is opened.
///
/// Columns, left to right: a fixed work-item-number column, a title column
/// (whose text indents by depth, with the expand/collapse caret in its own
/// fixed gutter before it), then Status and Assigned To — every column
/// (other than the caret gutter) independently resizable by dragging the
/// header dividers. Column widths are pure view state, like the collapse
/// set below — not persisted. When the columns are wider than the screen,
/// header and rows scroll horizontally together.
class HierarchyView extends StatefulWidget {
  const HierarchyView({
    super.key,
    required this.viewModel,
    required this.onItemOpened,
    required this.onAssignRequested,
  });

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;
  final void Function(String workItemId, String? currentAssigneeId)
  onAssignRequested;

  @override
  State<HierarchyView> createState() => _HierarchyViewState();
}

class _HierarchyViewState extends State<HierarchyView> {
  final Set<String> _collapsedIds = {};
  HierarchyColumnWidths _columnWidths = HierarchyColumnWidths();

  void _resizeColumn(HierarchyColumn column, double deltaX) {
    setState(() => _columnWidths = _columnWidths.resized(column, deltaX));
  }

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

    final rows = <HierarchyRow>[];
    void flatten(List<HierarchyNode> nodes, int depth) {
      for (final node in nodes) {
        rows.add(HierarchyRow(node: node, depth: depth));
        if (node.children.isNotEmpty && !_collapsedIds.contains(node.item.id)) {
          flatten(node.children, depth + 1);
        }
      }
    }

    flatten(viewModel.hierarchyRoots, 0);

    final table = Column(
      children: [
        HierarchyHeaderRow(
          columnWidths: _columnWidths,
          onResize: _resizeColumn,
        ),
        Expanded(
          child: rows.isEmpty
              ? const Center(
                  child: Text('No work items match the current filters.'),
                )
              : ListView.builder(
                  itemCount: rows.length,
                  itemBuilder: (context, index) {
                    final row = rows[index];
                    final node = row.node;
                    return HierarchyItemTile(
                      node: node,
                      depth: row.depth,
                      columnWidths: _columnWidths,
                      isCollapsed: _collapsedIds.contains(node.item.id),
                      onToggleCollapsed: () => setState(() {
                        if (!_collapsedIds.remove(node.item.id)) {
                          _collapsedIds.add(node.item.id);
                        }
                      }),
                      onTap: () => widget.onItemOpened(node.item.id),
                      onAssignTapped: () => widget.onAssignRequested(
                        node.item.id,
                        node.item.assignedToUserId,
                      ),
                      viewModel: viewModel,
                    );
                  },
                ),
        ),
      ],
    );

    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth > _columnWidths.rowWidth
            ? constraints.maxWidth
            : _columnWidths.rowWidth;
        return SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: SizedBox(width: width, child: table),
        );
      },
    );
  }
}
