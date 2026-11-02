import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/state/swimlane_edits.dart';

WorkItemCard _card(String id, String parentId, {String statusId = 'todo'}) =>
    WorkItemCard(
      id: id,
      number: 1,
      title: id,
      parentId: parentId,
      statusId: statusId,
    );

void main() {
  final lanes = [
    Swimlane(
      parentId: 'a',
      title: 'A',
      cards: [_card('1', 'a'), _card('2', 'a'), _card('3', 'a')],
    ),
    Swimlane(parentId: 'b', title: 'B', cards: [_card('4', 'b')]),
  ];

  test('moving a card appends it to the target lane and reparents it', () {
    final moved = lanes.withCardMovedToLane('2', 'a', 'b');

    expect(moved[0].cards.map((c) => c.id), ['1', '3']);
    expect(moved[1].cards.map((c) => c.id), ['4', '2']);
    expect(moved[1].cards.last.parentId, 'b');
  });

  test('moving back to an index restores the original position', () {
    final restored = lanes
        .withCardMovedToLane('2', 'a', 'b')
        .withCardMovedToLane('2', 'b', 'a', atIndex: 1);

    expect(restored[0].cards.map((c) => c.id), ['1', '2', '3']);
  });

  test('moving a card that is no longer in the source lane is a no-op', () {
    expect(lanes.withCardMovedToLane('4', 'a', 'b'), same(lanes));
  });

  test('assigning updates the lane item and matching cards', () {
    final assigned = lanes.withAssignee('a', 'u1').withAssignee('4', 'u2');

    expect(assigned[0].assignedToUserId, 'u1');
    expect(assigned.assigneeOf('a'), 'u1');
    expect(assigned.assigneeOf('4'), 'u2');
    expect(assigned.assigneeOf('1'), isNull);
  });

  group('placing a card in a status cell', () {
    // Lane a's cells, in rank order: todo = 1, 2, 3; doing = d1, d2.
    final mixed = [
      Swimlane(
        parentId: 'a',
        title: 'A',
        cards: [
          _card('1', 'a'),
          _card('d1', 'a', statusId: 'doing'),
          _card('2', 'a'),
          _card('d2', 'a', statusId: 'doing'),
          _card('3', 'a'),
        ],
      ),
    ];

    List<String> cell(List<Swimlane> lanes, String statusId) => [
      for (final card in lanes.single.cards)
        if (card.statusId == statusId) card.id,
    ];

    test('dropping before a card anchors after its predecessor', () {
      expect(mixed.rankAnchorForDrop('3', 'todo', beforeCardId: '2'), '1');
    });

    test('dropping before the first card has no anchor', () {
      expect(mixed.rankAnchorForDrop('3', 'todo', beforeCardId: '1'), isNull);
    });

    test('the dragged card is not its own anchor', () {
      expect(mixed.rankAnchorForDrop('2', 'todo', beforeCardId: '3'), '1');
    });

    test('dropping a card before itself keeps its current anchor', () {
      expect(mixed.rankAnchorForDrop('2', 'todo', beforeCardId: '2'), '1');
    });

    test('dropping at the end anchors after the last other card', () {
      expect(mixed.rankAnchorForDrop('3', 'todo'), '2');
      expect(mixed.rankAnchorForDrop('1', 'doing'), 'd2');
    });

    test('dropping at the end of an empty cell has no anchor', () {
      expect(mixed.rankAnchorForDrop('1', 'done'), isNull);
    });

    test('the anchor ignores cards in other statuses', () {
      expect(mixed.rankAnchorForDrop('1', 'doing', beforeCardId: 'd2'), 'd1');
    });

    test('a card is placed right after its anchor', () {
      final placed = mixed.withCardPlaced('3', 'todo', afterId: '1');

      expect(cell(placed, 'todo'), ['1', '3', '2']);
      expect(cell(placed, 'doing'), ['d1', 'd2']);
    });

    test('a card with no anchor goes to the top of the cell', () {
      final placed = mixed.withCardPlaced('3', 'todo', afterId: null);

      expect(cell(placed, 'todo'), ['3', '1', '2']);
    });

    test('placing in another status changes the card status', () {
      final placed = mixed.withCardPlaced('1', 'doing', afterId: 'd1');

      expect(cell(placed, 'todo'), ['2', '3']);
      expect(cell(placed, 'doing'), ['d1', '1', 'd2']);
    });

    test('an anchor that has left the cell puts the card at the top', () {
      final placed = mixed.withCardPlaced('3', 'doing', afterId: '1');

      expect(cell(placed, 'doing'), ['3', 'd1', 'd2']);
    });

    test('the cards before a card are read from its own cell', () {
      expect(mixed.cellIdsBefore('3'), ['1', '2']);
      expect(mixed.cellIdsBefore('d1'), isEmpty);
    });

    test('restoring puts the card back after its nearest remaining '
        'predecessor', () {
      final restored = mixed
          .withCardPlaced('3', 'todo', afterId: null)
          .withCardPlaced('2', 'doing', afterId: 'd2')
          .withCardRestored('3', 'todo', predecessorIds: ['1', '2']);

      expect(cell(restored, 'todo'), ['1', '3']);
      expect(cell(restored, 'doing'), ['d1', 'd2', '2']);
    });

    test('restoring with no predecessor left puts the card at the top', () {
      final restored = mixed
          .withCardPlaced('2', 'doing', afterId: null)
          .withCardRestored('2', 'todo', predecessorIds: ['x']);

      expect(cell(restored, 'todo'), ['2', '1', '3']);
    });

    test('placing a card that is not on the board is a no-op', () {
      expect(mixed.withCardPlaced('x', 'todo', afterId: '1'), same(mixed));
    });
  });
}
