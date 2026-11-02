import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

Map<String, dynamic> _entryJson({
  String field = 'title',
  Object? oldValue,
  Object? newValue,
  String? oldName,
  String? newName,
  String? accountName = 'alice',
  String? accountKind = 'Human',
  String? apiKeyLabel,
  String? actorLabel,
}) => {
  'id': 7,
  'workItemId': 'item-1',
  'changedAtUtc': '2026-09-01T12:00:00Z',
  'accountId': accountName == null ? null : 'user-1',
  'accountName': accountName,
  'accountKind': accountKind,
  'apiKeyId': apiKeyLabel == null ? null : 'key-1',
  'apiKeyLabel': apiKeyLabel,
  'actorLabel': actorLabel,
  'field': field,
  'oldValue': oldValue,
  'newValue': newValue,
  'oldName': oldName,
  'newName': newName,
};

String _change(Map<String, dynamic> json) =>
    WorkItemHistoryEntry.fromJson(json).change;

void main() {
  group('who', () {
    test('a human is named, with no agent badge', () {
      final entry = WorkItemHistoryEntry.fromJson(_entryJson());

      expect(entry.actorName, 'alice');
      expect(entry.isAgent, isFalse);
      expect(entry.actorDetail, isNull);
    });

    test('an agent carries its badge, key and actor label', () {
      final entry = WorkItemHistoryEntry.fromJson(
        _entryJson(
          accountName: 'Claude Code',
          accountKind: 'Agent',
          apiKeyLabel: 'laptop',
          actorLabel: 'sub-agent: test-writer',
        ),
      );

      expect(entry.actorName, 'Claude Code');
      expect(entry.isAgent, isTrue);
      expect(entry.actorDetail, 'sub-agent: test-writer · key "laptop"');
    });

    test('no account means Weaver itself', () {
      final entry = WorkItemHistoryEntry.fromJson(
        _entryJson(accountName: null, accountKind: null),
      );

      expect(entry.actorName, 'Weaver');
      expect(entry.isAgent, isFalse);
    });
  });

  group('change', () {
    test('a field change reads old → new', () {
      expect(
        _change(_entryJson(oldValue: 'Task', newValue: 'Renamed')),
        'Title: Task → Renamed',
      );
    });

    test('an id value shows its name, and a cleared one shows none', () {
      expect(
        _change(
          _entryJson(
            field: 'statusId',
            oldValue: 'todo-id',
            newValue: 'doing-id',
            oldName: 'To Do',
            newName: 'Doing',
          ),
        ),
        'Status: To Do → Doing',
      );
      expect(
        _change(
          _entryJson(field: 'assignedToUserId', oldValue: 'u', oldName: 'bob'),
        ),
        'Assignee: bob → none',
      );
    });

    test('tags read as a list', () {
      expect(
        _change(
          _entryJson(
            field: 'tags',
            oldValue: <String>[],
            newValue: ['urgent', 'later'],
          ),
        ),
        'Tags: none → urgent, later',
      );
    });

    test('create and delete name the title', () {
      expect(
        _change(_entryJson(field: 'created', newValue: 'Task')),
        'Created "Task"',
      );
      expect(
        _change(_entryJson(field: 'deleted', oldValue: 'Task')),
        'Deleted "Task"',
      );
    });

    test('comments read as added, edited or deleted', () {
      expect(
        _change(_entryJson(field: 'comment', newValue: 'Looks good')),
        'Added a comment: Looks good',
      );
      expect(
        _change(_entryJson(field: 'comment', oldValue: 'A', newValue: 'B')),
        'Edited a comment: A → B',
      );
      expect(
        _change(_entryJson(field: 'comment', oldValue: 'Looks good')),
        'Deleted a comment: Looks good',
      );
    });

    test('links name the other item', () {
      expect(
        _change(_entryJson(field: 'link', newValue: 'x', newName: '#4 Other')),
        'Linked #4 Other',
      );
      expect(
        _change(_entryJson(field: 'link', oldValue: 'x', oldName: '#4 Other')),
        'Unlinked #4 Other',
      );
    });

    test('a repetition reads as its schedule', () {
      expect(
        _change(
          _entryJson(
            field: 'recurrence',
            newValue: {
              'frequency': 'Weekly',
              'daysOfWeek': ['Monday', 'Friday'],
              'startDate': '2027-01-04',
              'endDate': null,
            },
          ),
        ),
        'Started repeating: Weekly on Monday, Friday from 2027-01-04',
      );
      expect(
        _change(
          _entryJson(
            field: 'recurrence',
            oldValue: {
              'frequency': 'Monthly',
              'daysOfWeek': <String>[],
              'startDate': '2027-01-04',
              'endDate': '2027-12-31',
            },
          ),
        ),
        'Stopped repeating: Monthly from 2027-01-04 until 2027-12-31',
      );
    });

    test('an unknown field falls back to its name', () {
      expect(
        _change(_entryJson(field: 'colour', oldValue: 'red', newValue: 2)),
        'colour: red → 2',
      );
    });
  });
}
