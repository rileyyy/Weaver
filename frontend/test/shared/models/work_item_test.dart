import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

Map<String, dynamic> _itemJson({
  Object? completionDate = _absent,
  Object? blockedBy = _absent,
}) => {
  'id': 'item-1',
  'number': 1,
  'spaceId': 'home',
  'parentId': null,
  'title': 'Paint the fence',
  'description': null,
  'statusId': 'done',
  'typeId': null,
  'priorityId': null,
  'assignedToUserId': null,
  'startDate': null,
  'endDate': null,
  'createdAtUtc': '2026-09-01T12:00:00Z',
  'updatedAtUtc': '2026-09-01T12:00:00Z',
  'version': 3,
  if (!identical(completionDate, _absent)) 'completionDate': completionDate,
  if (!identical(blockedBy, _absent)) 'blockedBy': blockedBy,
};

const Object _absent = Object();

void main() {
  group('completionDate', () {
    test('is read as a calendar day', () {
      final item = WorkItem.fromJson(_itemJson(completionDate: '2026-09-03'));

      expect(item.completionDate, DateTime(2026, 9, 3));
    });

    test('is null when the server sends null', () {
      final item = WorkItem.fromJson(_itemJson(completionDate: null));

      expect(item.completionDate, isNull);
    });

    test('is null from a server that predates it', () {
      final item = WorkItem.fromJson(_itemJson());

      expect(item.completionDate, isNull);
    });

    test('survives an optimistic edit', () {
      final item = WorkItem.fromJson(_itemJson(completionDate: '2026-09-03'));

      expect(item.withStatus('todo').completionDate, DateTime(2026, 9, 3));
    });
  });

  group('blockedBy', () {
    test('reads each live blocker with whether it conflicts', () {
      final item = WorkItem.fromJson(
        _itemJson(
          blockedBy: [
            {
              'id': 'drywall',
              'number': 12,
              'title': 'Drywall',
              'conflicts': true,
            },
            {
              'id': 'permit',
              'number': 3,
              'title': 'Permit',
              'conflicts': false,
            },
          ],
        ),
      );

      expect(item.blockedBy, [
        const WorkItemBlocker(
          id: 'drywall',
          number: 12,
          title: 'Drywall',
          conflicts: true,
        ),
        const WorkItemBlocker(
          id: 'permit',
          number: 3,
          title: 'Permit',
          conflicts: false,
        ),
      ]);
    });

    test('is empty from a server that predates it', () {
      expect(WorkItem.fromJson(_itemJson()).blockedBy, isEmpty);
    });

    test('survives an optimistic edit', () {
      final item = WorkItem.fromJson(
        _itemJson(
          blockedBy: [
            {
              'id': 'drywall',
              'number': 12,
              'title': 'Drywall',
              'conflicts': true,
            },
          ],
        ),
      );

      expect(
        item.rescheduled(DateTime(2026, 11, 1), null).blockedBy.single.id,
        'drywall',
      );
    });
  });
}
