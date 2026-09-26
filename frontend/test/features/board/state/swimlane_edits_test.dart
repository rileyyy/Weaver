import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/state/swimlane_edits.dart';

WorkItemCard _card(String id, String parentId) => WorkItemCard(
  id: id,
  number: 1,
  title: id,
  parentId: parentId,
  statusId: 'todo',
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
}
