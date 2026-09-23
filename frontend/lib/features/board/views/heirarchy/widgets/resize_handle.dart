import 'package:flutter/material.dart';

/// A narrow draggable divider between two header cells. Only the header
/// carries resize handles — body rows read the same [columnWidths] map, so
/// dragging a header divider resizes every row's cell in that column at
/// once.
class ResizeHandle extends StatelessWidget {
  static const double width = 8;

  const ResizeHandle({super.key, required this.onDrag});

  final ValueChanged<double> onDrag;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.resizeColumn,
      child: GestureDetector(
        behavior: HitTestBehavior.translucent,
        onHorizontalDragUpdate: (details) => onDrag(details.delta.dx),
        child: SizedBox(
          width: width,
          child: Center(
            child: Container(
              width: 1,
              color: Theme.of(context).colorScheme.outlineVariant,
            ),
          ),
        ),
      ),
    );
  }
}
