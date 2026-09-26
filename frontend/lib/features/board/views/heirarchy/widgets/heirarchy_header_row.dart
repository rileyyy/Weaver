import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_column_widths.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_layout.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/header_cell.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/resize_handle.dart';

class HierarchyHeaderRow extends StatelessWidget {
  const HierarchyHeaderRow({
    super.key,
    required this.columnWidths,
    required this.onResize,
  });

  final HierarchyColumnWidths columnWidths;
  final void Function(HierarchyColumn column, double deltaX) onResize;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(
      context,
    ).textTheme.labelLarge?.copyWith(fontWeight: FontWeight.w600);
    return Container(
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
      ),
      padding: const EdgeInsets.symmetric(
        vertical: 8,
        horizontal: hierarchyRowHorizontalPadding,
      ),
      child: Row(
        children: [
          const SizedBox(width: caretColumnWidth + caretGap),
          for (final column in HierarchyColumn.values) ...[
            HeaderCell(
              label: column.headerLabel,
              width: columnWidths[column],
              style: style,
            ),
            ResizeHandle(onDrag: (dx) => onResize(column, dx)),
          ],
          const Expanded(child: SizedBox()),
        ],
      ),
    );
  }
}
