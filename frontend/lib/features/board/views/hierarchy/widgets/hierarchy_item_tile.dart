import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_node.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_column.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_column_widths.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_layout.dart';
import 'package:weaver/features/board/views/hierarchy/widgets/column_value.dart';
import 'package:weaver/features/board/views/hierarchy/widgets/resize_handle.dart';

class HierarchyItemTile extends StatelessWidget {
  const HierarchyItemTile({
    super.key,
    required this.node,
    required this.depth,
    required this.columnWidths,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.onAssignTapped,
    required this.viewModel,
  });

  final HierarchyNode node;
  final int depth;
  final HierarchyColumnWidths columnWidths;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final VoidCallback onAssignTapped;
  final BoardViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          vertical: 6,
          horizontal: hierarchyRowHorizontalPadding,
        ),
        child: Row(
          children: [
            for (final column in HierarchyColumn.values) ...[
              SizedBox(width: columnWidths[column], child: _cell(column)),
              const SizedBox(width: ResizeHandle.width),
            ],
            const Expanded(child: SizedBox()),
          ],
        ),
      ),
    );
  }

  Widget _cell(HierarchyColumn column) {
    final value = ColumnValue(
      column: column,
      item: node.item,
      viewModel: viewModel,
      onAssignTapped: onAssignTapped,
    );
    // The caret lives inside the Title column and indents with the title,
    // as in a file tree; every other column keeps a fixed position so it
    // stays aligned under its header regardless of nesting.
    if (column != HierarchyColumn.title) return value;
    return Padding(
      padding: EdgeInsets.only(
        left: hierarchyTitleIndent(depth, columnWidths[column]),
      ),
      child: Row(
        children: [
          _caret(),
          const SizedBox(width: caretGap),
          Expanded(child: value),
        ],
      ),
    );
  }

  /// Fixed height/width whether or not this row has a caret, so titles
  /// line up at the same depth — and so IconButton's default 48x48 minimum
  /// tap target doesn't make rows with children taller than leaf rows.
  Widget _caret() => SizedBox(
    width: caretColumnWidth,
    height: caretColumnWidth,
    child: node.children.isEmpty
        ? null
        : IconButton(
            padding: EdgeInsets.zero,
            iconSize: 18,
            constraints: const BoxConstraints(),
            visualDensity: VisualDensity.compact,
            icon: Icon(isCollapsed ? Icons.chevron_right : Icons.expand_more),
            tooltip: isCollapsed ? 'Expand' : 'Collapse',
            onPressed: onToggleCollapsed,
          ),
  );
}
