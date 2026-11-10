import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/work_item.dart';

Map<String, dynamic> _itemJson({Object? completionDate = _absent}) => {
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
}
