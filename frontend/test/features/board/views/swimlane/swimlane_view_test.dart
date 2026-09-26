import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/views/swimlane/widgets/grid_row_box.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/shared/models/work_item_status.dart';

final String _longTitle = List.filled(40, 'word').join(' ');

Widget _board(double width, {double textScale = 1}) => MaterialApp(
  home: MediaQuery(
    data: MediaQueryData(textScaler: TextScaler.linear(textScale)),
    child: Scaffold(
      body: SingleChildScrollView(
        child: SwimlaneView(
          width: width,
          statuses: const [
            WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
            WorkItemStatus(id: 'done', name: 'Done', order: 1),
          ],
          swimlanes: [
            Swimlane(
              parentId: 'lane-a',
              title: 'Lane A',
              cards: [
                WorkItemCard(
                  id: 'long',
                  number: 1,
                  title: _longTitle,
                  parentId: 'lane-a',
                  statusId: 'todo',
                  startDate: DateTime(2026, 9, 1),
                  endDate: DateTime(2026, 9, 30),
                  tags: const ['urgent', 'design', 'backend'],
                ),
              ],
            ),
            const Swimlane(parentId: 'lane-b', title: 'Lane B', cards: []),
          ],
          onCardDropped: (_, _) async {},
          onCardReparented: (_, _) async {},
          onCardDetailsOpened: (_) {},
          onSwimlaneLabelTapped: (_) {},
          onAssignRequested: (_, _) {},
          cardVisible: (_) => true,
          cardComparator: null,
          assigneeInitialFor: (_) => null,
          collapsedSwimlaneIds: const {},
          onToggleSwimlaneCollapsed: (_) {},
          onToggleAllSwimlanesCollapsed: () {},
        ),
      ),
    ),
  ),
);

void main() {
  for (final (label, width, scale) in [
    ('narrow', 400.0, 1.0),
    ('wide', 1280.0, 1.0),
    ('wide with large text', 1280.0, 1.5),
  ]) {
    testWidgets('a long title stays inside its card and lane ($label)', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 1600);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(_board(width, textScale: scale));

      // A RenderFlex overflow would be reported here.
      expect(tester.takeException(), isNull);
      final cardRect = tester.getRect(find.byType(BoardCard));
      final laneRow = find.ancestor(
        of: find.byType(BoardCard),
        matching: find.byType(GridRowBox),
      );
      expect(
        cardRect.bottom,
        lessThanOrEqualTo(tester.getRect(laneRow).bottom),
      );
    });
  }
}
