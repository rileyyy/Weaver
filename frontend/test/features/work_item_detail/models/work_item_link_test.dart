import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_type.dart';

Map<String, dynamic> _json({String type = 'Related'}) => {
  'id': 'link-1',
  'type': type,
  'linkedWorkItemId': 'item-2',
  'linkedWorkItemNumber': 12,
  'linkedWorkItemTitle': 'Hang drywall',
  'linkedWorkItemSpaceId': 'house',
};

void main() {
  test('parses the other item and the type seen from this one', () {
    final link = WorkItemLink.fromJson(_json(type: 'BlockedBy'));

    expect(link.id, 'link-1');
    expect(link.type, WorkItemLinkType.blockedBy);
    expect(link.linkedWorkItemId, 'item-2');
    expect(link.linkedWorkItemNumber, 12);
    expect(link.linkedWorkItemTitle, 'Hang drywall');
    expect(link.linkedWorkItemSpaceId, 'house');
  });

  test('parses every link type', () {
    expect(
      [
        for (final wire in [
          'Related',
          'Blocks',
          'BlockedBy',
          'Duplicates',
          'DuplicatedBy',
        ])
          WorkItemLink.fromJson(_json(type: wire)).type,
      ],
      [
        WorkItemLinkType.related,
        WorkItemLinkType.blocks,
        WorkItemLinkType.blockedBy,
        WorkItemLinkType.duplicates,
        WorkItemLinkType.duplicatedBy,
      ],
    );
  });

  test('rejects an unknown type rather than guessing', () {
    expect(
      () => WorkItemLink.fromJson(_json(type: 'Parent')),
      throwsFormatException,
    );
  });

  test("labels each type from this item's side", () {
    expect(
      [for (final type in WorkItemLinkType.values) type.label],
      ['Related', 'Blocks', 'Blocked by', 'Duplicates', 'Duplicated by'],
    );
  });
}
