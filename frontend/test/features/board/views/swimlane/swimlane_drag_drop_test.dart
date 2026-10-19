import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/platform/touch_platform.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/views/swimlane/widgets/status_column.dart';
import 'package:weaver/features/board/views/swimlane/widgets/swimlane_label.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/shared/models/work_item_status.dart';

const _card = WorkItemCard(
  id: 'card-1',
  number: 1,
  title: 'Drag me',
  parentId: 'lane-a',
  statusId: 'todo',
);

/// Drop-target behaviour must not depend on how a drag is started, so it
/// runs on one touch and one pointer platform.
const _touchAndDesktop = TargetPlatformVariant({
  TargetPlatform.android,
  TargetPlatform.macOS,
});

void main() {
  late List<(String, String)> statusDrops;
  late List<(String, String)> reparents;

  Future<void> pumpBoard(WidgetTester tester) async {
    statusDrops = [];
    reparents = [];
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: SwimlaneView(
              width: 1280,
              statuses: const [
                WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
                WorkItemStatus(id: 'done', name: 'Done', order: 1),
              ],
              swimlanes: const [
                Swimlane(parentId: 'lane-a', title: 'Lane A', cards: [_card]),
                Swimlane(parentId: 'lane-b', title: 'Lane B', cards: []),
              ],
              onCardDropped: (card, statusId) async =>
                  statusDrops.add((card.id, statusId)),
              onCardReparented: (card, parentId) async =>
                  reparents.add((card.id, parentId)),
              onCardDetailsOpened: (_) {},
              onSwimlaneLabelTapped: (_) {},
              onAssignRequested: (_, _) {},
              onAddTagsRequested: (_, _) {},
              cardVisible: (_) => true,
              cardComparator: null,
              assigneeInitialFor: (_) => null,
              layerColorFor: (_) => null,
              collapsedSwimlaneIds: const {},
              onToggleSwimlaneCollapsed: (_) {},
              onToggleAllSwimlanesCollapsed: () {},
            ),
          ),
        ),
      ),
    );
  }

  Finder cell(String laneId, String statusId) => find.byWidgetPredicate(
    (w) =>
        w is StatusColumn &&
        w.swimlane.parentId == laneId &&
        w.status.id == statusId,
  );

  Finder label(String laneId) => find.byWidgetPredicate(
    (w) => w is SwimlaneLabel && w.swimlane.parentId == laneId,
  );

  Future<void> dragCardTo(
    WidgetTester tester,
    Finder target, {
    bool holdFirst = true,
  }) async {
    final gesture = await tester.startGesture(
      tester.getCenter(find.byType(BoardCard)),
    );
    await tester.pump();
    if (holdFirst && isTouchPlatform) {
      await tester.pump(kLongPressTimeout + const Duration(milliseconds: 50));
    }
    await gesture.moveTo(tester.getCenter(target));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();
  }

  testWidgets(
    'dropping on another status in the same lane moves the card',
    (tester) async {
      await pumpBoard(tester);

      await dragCardTo(tester, cell('lane-a', 'done'));

      expect(statusDrops, [('card-1', 'done')]);
      expect(reparents, isEmpty);
    },
    variant: _touchAndDesktop,
  );

  testWidgets('a status cell in another lane rejects the card', (tester) async {
    await pumpBoard(tester);

    await dragCardTo(tester, cell('lane-b', 'done'));

    expect(statusDrops, isEmpty);
    expect(reparents, isEmpty);
  }, variant: _touchAndDesktop);

  testWidgets('dropping on another lane label reparents the card', (
    tester,
  ) async {
    await pumpBoard(tester);

    await dragCardTo(tester, label('lane-b'));

    expect(reparents, [('card-1', 'lane-b')]);
    expect(statusDrops, isEmpty);
  }, variant: _touchAndDesktop);

  testWidgets("the card's own lane label rejects it", (tester) async {
    await pumpBoard(tester);

    await dragCardTo(tester, label('lane-a'));

    expect(reparents, isEmpty);
  }, variant: _touchAndDesktop);

  testWidgets(
    'on touch platforms a swipe that starts on a card does not move it',
    (tester) async {
      await pumpBoard(tester);

      await dragCardTo(tester, cell('lane-a', 'done'), holdFirst: false);

      expect(statusDrops, isEmpty);
      expect(find.byType(LongPressDraggable<WorkItemCard>), findsOneWidget);
    },
    variant: TargetPlatformVariant.only(TargetPlatform.android),
  );

  testWidgets(
    'on desktop platforms a card moves without a long press',
    (tester) async {
      await pumpBoard(tester);

      await dragCardTo(tester, cell('lane-a', 'done'), holdFirst: false);

      expect(statusDrops, [('card-1', 'done')]);
    },
    variant: TargetPlatformVariant.only(TargetPlatform.windows),
  );
}
