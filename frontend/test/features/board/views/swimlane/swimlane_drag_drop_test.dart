import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/platform/touch_platform.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_layout.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/views/swimlane/widgets/card_drop_zones.dart';
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

WorkItemCard _cardIn(String id, String laneId, String statusId) => WorkItemCard(
  id: id,
  number: 1,
  title: id,
  parentId: laneId,
  statusId: statusId,
);

/// Drop-target behaviour must not depend on how a drag is started, so it
/// runs on one touch and one pointer platform.
const _touchAndDesktop = TargetPlatformVariant({
  TargetPlatform.android,
  TargetPlatform.macOS,
});

void main() {
  late List<(String, String, String?)> statusDrops;
  late List<(String, String)> reparents;

  Future<void> pumpBoard(
    WidgetTester tester, {
    List<WorkItemCard> laneACards = const [_card],
    List<WorkItemCard> laneBCards = const [],
    Comparator<WorkItemCard>? cardComparator,
  }) async {
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
              swimlaneListenables: [
                ValueNotifier(
                  Swimlane(
                    parentId: 'lane-a',
                    title: 'Lane A',
                    cards: laneACards,
                  ),
                ),
                ValueNotifier(
                  Swimlane(
                    parentId: 'lane-b',
                    title: 'Lane B',
                    cards: laneBCards,
                  ),
                ),
              ],
              onCardDropped: (card, statusId, {beforeCardId}) async =>
                  statusDrops.add((card.id, statusId, beforeCardId)),
              onCardReparented: (card, parentId) async =>
                  reparents.add((card.id, parentId)),
              onCardDetailsOpened: (_) {},
              onSwimlaneLabelTapped: (_) {},
              onAssignRequested: (_, _) {},
              onAddTagsRequested: (_, _) {},
              cardVisible: (_) => true,
              cardComparator: cardComparator,
              assigneeInitialFor: (_) => null,
              typeColorFor: (_) => null,
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

  Finder boardCard(String cardId) =>
      find.byWidgetPredicate((w) => w is BoardCard && w.card.id == cardId);

  Offset leftHalfOf(WidgetTester tester, Finder finder) {
    final rect = tester.getRect(finder);
    return Offset(rect.left + rect.width / 4, rect.center.dy);
  }

  Offset rightHalfOf(WidgetTester tester, Finder finder) {
    final rect = tester.getRect(finder);
    return Offset(rect.right - rect.width / 4, rect.center.dy);
  }

  Future<TestGesture> pickUp(
    WidgetTester tester, {
    String cardId = 'card-1',
    bool holdFirst = true,
  }) async {
    final gesture = await tester.startGesture(
      tester.getCenter(boardCard(cardId)),
    );
    await tester.pump();
    if (holdFirst && isTouchPlatform) {
      await tester.pump(kLongPressTimeout + const Duration(milliseconds: 50));
    }
    return gesture;
  }

  Future<void> dragCardToPoint(
    WidgetTester tester,
    Offset point, {
    String cardId = 'card-1',
    bool holdFirst = true,
  }) async {
    final gesture = await pickUp(tester, cardId: cardId, holdFirst: holdFirst);
    await gesture.moveTo(point);
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();
  }

  Future<void> dragCardTo(
    WidgetTester tester,
    Finder target, {
    bool holdFirst = true,
  }) => dragCardToPoint(tester, tester.getCenter(target), holdFirst: holdFirst);

  testWidgets(
    'dropping on another status in the same lane moves the card',
    (tester) async {
      await pumpBoard(tester);

      await dragCardTo(tester, cell('lane-a', 'done'));

      expect(statusDrops, [('card-1', 'done', null)]);
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

      expect(statusDrops, [('card-1', 'done', null)]);
    },
    variant: TargetPlatformVariant.only(TargetPlatform.windows),
  );

  group('dropping onto a card', () {
    // Cards sit two to a row, so lane A's To Do cell reads
    // card-1 card-2 / card-3.
    final threeInTodo = [
      _cardIn('card-1', 'lane-a', 'todo'),
      _cardIn('card-2', 'lane-a', 'todo'),
      _cardIn('card-3', 'lane-a', 'todo'),
      _cardIn('card-4', 'lane-a', 'done'),
    ];

    testWidgets(
      'on its leading half places the dragged card before it',
      (tester) async {
        await pumpBoard(tester, laneACards: threeInTodo);

        await dragCardToPoint(
          tester,
          leftHalfOf(tester, boardCard('card-1')),
          cardId: 'card-3',
        );

        expect(statusDrops, [('card-3', 'todo', 'card-1')]);
      },
      variant: _touchAndDesktop,
    );

    testWidgets(
      'on its trailing half places the dragged card before the next card',
      (tester) async {
        await pumpBoard(tester, laneACards: threeInTodo);

        await dragCardToPoint(tester, rightHalfOf(tester, boardCard('card-2')));

        expect(statusDrops, [('card-1', 'todo', 'card-3')]);
      },
      variant: _touchAndDesktop,
    );

    testWidgets("on the last card's trailing half places it at the end", (
      tester,
    ) async {
      await pumpBoard(tester, laneACards: threeInTodo);

      await dragCardToPoint(tester, rightHalfOf(tester, boardCard('card-3')));

      expect(statusDrops, [('card-1', 'todo', null)]);
    });

    testWidgets('shows a drop indicator only while hovering a new position', (
      tester,
    ) async {
      await pumpBoard(tester, laneACards: threeInTodo);
      final gesture = await pickUp(tester, cardId: 'card-3');

      await gesture.moveTo(leftHalfOf(tester, boardCard('card-1')));
      await tester.pump();
      expect(find.byKey(cardDropIndicatorKey), findsOneWidget);

      // After card-2 is where card-3 already is.
      await gesture.moveTo(rightHalfOf(tester, boardCard('card-2')));
      await tester.pump();
      expect(find.byKey(cardDropIndicatorKey), findsNothing);

      await gesture.up();
      await tester.pumpAndSettle();
      expect(find.byKey(cardDropIndicatorKey), findsNothing);
      expect(statusDrops, isEmpty);
    });

    testWidgets('onto itself does nothing', (tester) async {
      await pumpBoard(tester, laneACards: threeInTodo);

      await dragCardToPoint(
        tester,
        leftHalfOf(tester, boardCard('card-2')),
        cardId: 'card-2',
      );

      expect(statusDrops, isEmpty);
    }, variant: _touchAndDesktop);

    testWidgets(
      'in another column moves the card to that position',
      (tester) async {
        await pumpBoard(tester, laneACards: threeInTodo);

        await dragCardToPoint(tester, leftHalfOf(tester, boardCard('card-4')));

        expect(statusDrops, [('card-1', 'done', 'card-4')]);
      },
      variant: _touchAndDesktop,
    );

    testWidgets('in another lane does nothing', (tester) async {
      await pumpBoard(
        tester,
        laneACards: threeInTodo,
        laneBCards: [_cardIn('card-5', 'lane-b', 'todo')],
      );

      await dragCardToPoint(tester, leftHalfOf(tester, boardCard('card-5')));

      expect(statusDrops, isEmpty);
      expect(reparents, isEmpty);
    });

    testWidgets(
      'while a sort is applied, only moves between columns, to the end',
      (tester) async {
        await pumpBoard(
          tester,
          laneACards: threeInTodo,
          cardComparator: (a, b) => a.id.compareTo(b.id),
        );

        await dragCardToPoint(
          tester,
          leftHalfOf(tester, boardCard('card-1')),
          cardId: 'card-3',
        );
        await dragCardToPoint(tester, leftHalfOf(tester, boardCard('card-4')));

        expect(statusDrops, [('card-1', 'done', null)]);
      },
    );
  });

  group('dropping in a gap', () {
    // card-1 card-2 / card-3, as above.
    final threeInTodo = [
      _cardIn('card-1', 'lane-a', 'todo'),
      _cardIn('card-2', 'lane-a', 'todo'),
      _cardIn('card-3', 'lane-a', 'todo'),
    ];
    const quarterGap = cardGridSpacing / 4;

    testWidgets('between two cards in a row lands between them', (
      tester,
    ) async {
      await pumpBoard(tester, laneACards: threeInTodo);
      final first = tester.getRect(boardCard('card-1'));
      final second = tester.getRect(boardCard('card-2'));

      await dragCardToPoint(
        tester,
        Offset(first.right + quarterGap, first.center.dy),
        cardId: 'card-3',
      );
      await dragCardToPoint(
        tester,
        Offset(second.left - quarterGap, second.center.dy),
        cardId: 'card-3',
      );

      expect(statusDrops, [
        ('card-3', 'todo', 'card-2'),
        ('card-3', 'todo', 'card-2'),
      ]);
    }, variant: _touchAndDesktop);

    testWidgets('above a card lands before it', (tester) async {
      await pumpBoard(tester, laneACards: threeInTodo);

      await dragCardToPoint(
        tester,
        leftHalfOf(tester, boardCard('card-3')) -
            Offset(0, tester.getRect(boardCard('card-3')).height / 2) -
            const Offset(0, quarterGap),
      );

      expect(statusDrops, [('card-1', 'todo', 'card-3')]);
    });

    testWidgets('shows the indicator while hovering the gap', (tester) async {
      await pumpBoard(tester, laneACards: threeInTodo);
      final first = tester.getRect(boardCard('card-1'));
      final gesture = await pickUp(tester, cardId: 'card-3');

      await gesture.moveTo(Offset(first.right + quarterGap, first.center.dy));
      await tester.pump();

      expect(find.byKey(cardDropIndicatorKey), findsOneWidget);
      await gesture.up();
      await tester.pumpAndSettle();
    });
  });

  group('dropping on free space in its own cell', () {
    final twoInTodo = [
      _cardIn('card-1', 'lane-a', 'todo'),
      _cardIn('card-2', 'lane-a', 'todo'),
    ];

    Offset freeSpace(WidgetTester tester) {
      final cellRect = tester.getRect(cell('lane-a', 'todo'));
      return Offset(cellRect.center.dx, cellRect.bottom - 10);
    }

    testWidgets('moves the card to the end', (tester) async {
      await pumpBoard(tester, laneACards: twoInTodo);

      await dragCardToPoint(tester, freeSpace(tester));

      expect(statusDrops, [('card-1', 'todo', null)]);
    });

    testWidgets('shows the indicator at the end, unless the card is there', (
      tester,
    ) async {
      await pumpBoard(tester, laneACards: twoInTodo);

      final first = await pickUp(tester);
      await first.moveTo(freeSpace(tester));
      await tester.pump();
      expect(find.byKey(cardDropIndicatorKey), findsOneWidget);
      expect(
        tester.getCenter(find.byKey(cardDropIndicatorKey)).dx,
        greaterThan(tester.getRect(boardCard('card-2')).right),
      );
      await first.up();
      await tester.pumpAndSettle();

      final last = await pickUp(tester, cardId: 'card-2');
      await last.moveTo(freeSpace(tester));
      await tester.pump();
      expect(find.byKey(cardDropIndicatorKey), findsNothing);
      await last.up();
      await tester.pumpAndSettle();
    });
  });
}
