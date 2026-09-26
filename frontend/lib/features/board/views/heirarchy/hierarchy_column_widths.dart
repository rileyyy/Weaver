import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_layout.dart';

/// Current width of every Hierarchy column. Immutable; resizing returns a
/// new value. Pure view state, not persisted.
class HierarchyColumnWidths {
  HierarchyColumnWidths() : _widths = const {};

  HierarchyColumnWidths._(this._widths);

  static const double minWidth = 56;
  static const double maxWidth = 640;

  final Map<HierarchyColumn, double> _widths;

  double operator [](HierarchyColumn column) =>
      _widths[column] ?? column.defaultWidth;

  HierarchyColumnWidths resized(HierarchyColumn column, double deltaX) =>
      HierarchyColumnWidths._({
        ..._widths,
        column: (this[column] + deltaX).clamp(minWidth, maxWidth),
      });

  /// Width of a full row: padding, caret gutter, every column and the
  /// resize handle after each. Beyond this the view scrolls horizontally.
  double get rowWidth =>
      2 * hierarchyRowHorizontalPadding +
      caretColumnWidth +
      caretGap +
      HierarchyColumn.values.fold(
        0.0,
        (sum, column) => sum + this[column] + hierarchyResizeHandleWidth,
      );
}
