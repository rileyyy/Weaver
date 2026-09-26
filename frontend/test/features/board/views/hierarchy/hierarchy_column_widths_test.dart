import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_column_widths.dart';

void main() {
  test('every column starts at its default width', () {
    final widths = HierarchyColumnWidths();

    for (final column in HierarchyColumn.values) {
      expect(widths[column], column.defaultWidth);
    }
  });

  test('resizing returns a new value and leaves other columns alone', () {
    final original = HierarchyColumnWidths();

    final resized = original.resized(HierarchyColumn.title, 40);

    expect(
      resized[HierarchyColumn.title],
      HierarchyColumn.title.defaultWidth + 40,
    );
    expect(
      resized[HierarchyColumn.status],
      HierarchyColumn.status.defaultWidth,
    );
    expect(original[HierarchyColumn.title], HierarchyColumn.title.defaultWidth);
  });

  test('widths are clamped to the allowed range', () {
    final widths = HierarchyColumnWidths()
        .resized(HierarchyColumn.number, -1000)
        .resized(HierarchyColumn.tags, 5000);

    expect(widths[HierarchyColumn.number], HierarchyColumnWidths.minWidth);
    expect(widths[HierarchyColumn.tags], HierarchyColumnWidths.maxWidth);
  });

  test('rowWidth grows with the columns', () {
    final widths = HierarchyColumnWidths();

    expect(
      widths.resized(HierarchyColumn.title, 100).rowWidth,
      widths.rowWidth + 100,
    );
  });
}
