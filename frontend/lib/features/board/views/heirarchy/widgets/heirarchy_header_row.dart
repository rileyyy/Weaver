import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_view.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/header_cell.dart';
import 'package:weaver/features/board/views/heirarchy/widgets/resize_handle.dart';

const double caretColumnWidth = 24;
const double caretGap = 8;

class HierarchyHeaderRow extends StatelessWidget {
  const HierarchyHeaderRow({
    super.key,
    required this.columnWidths,
    required this.onResize,
  });

  final Map<String, double> columnWidths;
  final void Function(String key, double deltaX) onResize;

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
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
      child: Row(
        children: [
          const SizedBox(width: caretColumnWidth + caretGap),
          HeaderCell(label: '#', width: columnWidths['number']!, style: style),
          ResizeHandle(onDrag: (dx) => onResize('number', dx)),
          HeaderCell(
            label: 'Title',
            width: columnWidths['title']!,
            style: style,
          ),
          ResizeHandle(onDrag: (dx) => onResize('title', dx)),
          for (final column in trailingColumns) ...[
            HeaderCell(
              label: column.headerLabel,
              width: columnWidths[column.name]!,
              style: style,
            ),
            ResizeHandle(onDrag: (dx) => onResize(column.name, dx)),
          ],
          const Expanded(child: SizedBox()),
        ],
      ),
    );
  }
}
