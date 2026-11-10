import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

WorkItemCard _card({
  String title = 'Card',
  String statusId = 'todo',
  String? assignedToUserId,
  DateTime? endDate,
  List<String> tags = const ['a'],
  List<WorkItemBlocker> blockedBy = const [],
}) => WorkItemCard(
  id: 'card',
  number: 1,
  title: title,
  parentId: 'lane',
  statusId: statusId,
  assignedToUserId: assignedToUserId,
  endDate: endDate,
  tags: tags,
  blockedBy: blockedBy,
);

Swimlane _lane({String title = 'Lane', List<WorkItemCard>? cards}) =>
    Swimlane(parentId: 'lane', title: title, cards: cards ?? [_card()]);

void main() {
  // A lane only rebuilds when it stops comparing equal, so a field missing
  // from == is a change the board silently never shows.
  test('lanes drawn alike are equal, with equal hash codes', () {
    expect(_lane(), _lane());
    expect(_lane().hashCode, _lane().hashCode);
  });

  for (final (what, changed) in [
    ('title', _card(title: 'Renamed')),
    ('status', _card(statusId: 'done')),
    ('assignee', _card(assignedToUserId: 'user')),
    ('end date', _card(endDate: DateTime(2026, 11, 3))),
    ('tags', _card(tags: const ['a', 'b'])),
    (
      'blockers',
      _card(
        blockedBy: const [
          WorkItemBlocker(
            id: 'drywall',
            number: 12,
            title: 'Drywall',
            spaceId: 'home',
            endDate: null,
            conflicts: true,
          ),
        ],
      ),
    ),
  ]) {
    test('a card differing only in its $what is a different card', () {
      expect(changed, isNot(_card()));
      expect(_lane(cards: [changed]), isNot(_lane()));
    });
  }

  test('a lane differing in its title or card order is a different lane', () {
    const other = WorkItemCard(
      id: 'other',
      number: 2,
      title: 'Other',
      parentId: 'lane',
      statusId: 'todo',
    );

    expect(_lane(title: 'Renamed'), isNot(_lane()));
    expect(
      _lane(cards: [_card(), other]),
      isNot(_lane(cards: [other, _card()])),
    );
  });
}
