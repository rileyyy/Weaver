import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_view.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/column_value.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/heirarchy_header_row.dart';
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
  final Map<String, double> columnWidths;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final VoidCallback onAssignTapped;
  final BoardViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final hasChildren = node.children.isNotEmpty;
    final item = node.item;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 12),
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
            SizedBox(
              width: columnWidths['number'],
              child: Text('#${item.number}', overflow: TextOverflow.ellipsis),
            ),
            const SizedBox(width: ResizeHandle.width),
            // Depth only indents the title's own text — not the caret or
            // number column before it, nor the columns after it — so every
            // column stays aligned under its header regardless of nesting.
            SizedBox(
              width: columnWidths['title'],
              child: Padding(
                padding: EdgeInsets.only(left: depth * _indentPerLevel),
                child: Text(item.title, overflow: TextOverflow.ellipsis),
              ),
            ),
            const SizedBox(width: ResizeHandle.width),
            for (final column in trailingColumns) ...[
              SizedBox(
                width: columnWidths[column.name],
                child: ColumnValue(
                  column: column,
                  item: item,
                  viewModel: viewModel,
                  onAssignTapped: onAssignTapped,
                ),
              ),
              const SizedBox(width: ResizeHandle.width),
            ],
            const Expanded(child: SizedBox()),
          ],
        ),
      ),
    );
  }
}
