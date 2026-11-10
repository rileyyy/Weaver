import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/flattened_tree_row.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/hierarchy_node.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_layout.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_row_tile.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

final _windowStart = DateTime(2026, 11, 2);

Widget _tile({DateTime? startDate, required bool conflicts}) => MaterialApp(
  home: Scaffold(
    body: RoadmapRowTile(
      row: FlattenedTreeRow(
        node: HierarchyNode(
          item: HierarchyItem(
            id: 'paint',
            number: 5,
            parentId: null,
            title: 'Paint',
            statusId: 'todo',
            startDate: startDate,
            endDate: startDate,
            blockedBy: [
              WorkItemBlocker(
                id: 'drywall',
                number: 4,
                title: 'Drywall',
                conflicts: conflicts,
              ),
            ],
          ),
          children: const [],
        ),
        depth: 0,
      ),
      timeframe: RoadmapTimeframe.fortnight,
      windowStart: _windowStart,
      timelineWidth: 420,
      isCollapsed: false,
      onToggleCollapsed: () {},
      onTap: () {},
      onAddTagTapped: () {},
      statusColorFor: (_) => null,
    ),
  ),
);

void main() {
  testWidgets('a conflicting bar shows the conflict badge', (tester) async {
    await tester.pumpWidget(
      _tile(startDate: DateTime(2026, 11, 5), conflicts: true),
    );

    expect(find.byIcon(Icons.warning_amber_rounded), findsOneWidget);
    expect(_badgeX(tester), greaterThanOrEqualTo(leftColumnWidth));
  });

  testWidgets('a bar without a conflict shows no badge', (tester) async {
    await tester.pumpWidget(
      _tile(startDate: DateTime(2026, 11, 5), conflicts: false),
    );

    expect(find.byIcon(Icons.warning_amber_rounded), findsNothing);
  });

  testWidgets('a conflicting item with no dates shows the badge by its '
      'title', (tester) async {
    await tester.pumpWidget(_tile(startDate: null, conflicts: true));

    expect(_badgeX(tester), lessThan(leftColumnWidth));
  });

  testWidgets('a conflicting item whose bar is outside the window shows the '
      'badge by its title', (tester) async {
    await tester.pumpWidget(
      _tile(startDate: DateTime(2026, 12, 20), conflicts: true),
    );

    expect(_badgeX(tester), lessThan(leftColumnWidth));
  });

  testWidgets('an item with no bar and no conflict shows no badge', (
    tester,
  ) async {
    await tester.pumpWidget(_tile(startDate: null, conflicts: false));

    expect(find.byIcon(Icons.warning_amber_rounded), findsNothing);
  });
}

double _badgeX(WidgetTester tester) =>
    tester.getTopLeft(find.byIcon(Icons.warning_amber_rounded)).dx;
