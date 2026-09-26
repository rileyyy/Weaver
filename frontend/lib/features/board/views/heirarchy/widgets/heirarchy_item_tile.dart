import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_column_widths.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_layout.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/column_value.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/resize_handle.dart';

class HierarchyItemTile extends StatelessWidget {
  static const double _indentPerLevel = 24;

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
    final hasChildren = node.children.isNotEmpty;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          vertical: 6,
          horizontal: hierarchyRowHorizontalPadding,
        ),
        child: Row(
          children: [
            // Fixed height/width regardless of whether this row has a
            // caret — IconButton's default 48x48 minimum tap target would
            // otherwise make rows with children taller than leaf rows. Its
            // position never shifts with depth — see the class doc comment.
            SizedBox(
              width: caretColumnWidth,
              height: caretColumnWidth,
              child: hasChildren
                  ? IconButton(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      constraints: const BoxConstraints(),
                      visualDensity: VisualDensity.compact,
                      icon: Icon(
                        isCollapsed ? Icons.chevron_right : Icons.expand_more,
                      ),
                      tooltip: isCollapsed ? 'Expand' : 'Collapse',
                      onPressed: onToggleCollapsed,
                    )
                  : null,
            ),
            const SizedBox(width: caretGap),
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
    // Depth only indents the title's own text — not the caret, the number
    // or the columns after it — so every column stays aligned under its
    // header regardless of nesting.
    if (column != HierarchyColumn.title) return value;
    return Padding(
      padding: EdgeInsets.only(left: depth * _indentPerLevel),
      child: value,
    );
  }
}
