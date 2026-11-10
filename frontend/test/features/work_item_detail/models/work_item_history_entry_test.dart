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
  String? fieldName,
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
  'fieldName': fieldName,
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

    test('the Space an item was created in reads by name', () {
      expect(
        _change(
          _entryJson(field: 'spaceId', newValue: 'space-1', newName: 'Home'),
        ),
        'In Space Home',
      );
    });

    test('a Space change is labelled Space', () {
      expect(
        _change(
          _entryJson(
            field: 'spaceId',
            oldValue: 'space-1',
            newValue: 'space-2',
            oldName: 'Home',
            newName: 'Village',
          ),
        ),
        'Space: Home → Village',
      );
    });

    test('a completion date change is labelled Completion date', () {
      expect(
        _change(
          _entryJson(
            field: 'completionDate',
            oldValue: null,
            newValue: '2026-09-03',
          ),
        ),
        'Completion date: none → 2026-09-03',
      );
    });

    test('a When change is labelled When', () {
      expect(
        _change(
          _entryJson(field: 'when', oldValue: null, newValue: 'after drywall'),
        ),
        'When: none → after drywall',
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

    test('a restore from the Trash names the item', () {
      expect(
        _change(_entryJson(field: 'restored', newValue: 'Task')),
        'Restored "Task" from the Trash',
      );
    });

    test('a purge names the item it deleted permanently', () {
      expect(
        _change(_entryJson(field: 'purged', oldValue: 'Task')),
        'Permanently deleted "Task" from the Trash',
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

    test('typed links name the type in words and the other item', () {
      expect(
        _change(
          _entryJson(
            field: 'link',
            newValue: {'workItemId': 'x', 'type': 'Blocks'},
            newName: '#4 Paint',
          ),
        ),
        'Linked: blocks #4 Paint',
      );
      expect(
        _change(
          _entryJson(
            field: 'link',
            oldValue: {'workItemId': 'x', 'type': 'BlockedBy'},
            oldName: '#4 Drywall',
          ),
        ),
        'Unlinked: blocked by #4 Drywall',
      );
      expect(
        _change(
          _entryJson(
            field: 'link',
            newValue: {'workItemId': 'x', 'type': 'Related'},
            newName: '#4 Other',
          ),
        ),
        'Linked: related to #4 Other',
      );
    });

    test('a typed link to a purged item falls back to its id', () {
      expect(
        _change(
          _entryJson(
            field: 'link',
            newValue: {'workItemId': 'x', 'type': 'DuplicatedBy'},
          ),
        ),
        'Linked: duplicated by x',
      );
    });

    test('a link of a type this app does not know still reads', () {
      expect(
        _change(
          _entryJson(
            field: 'link',
            newValue: {'workItemId': 'x', 'type': 'Causes'},
            newName: '#4 Other',
          ),
        ),
        'Linked #4 Other',
      );
    });

    test('URL links name the label, or the URL without one', () {
      expect(
        _change(
          _entryJson(
            field: 'urlLink',
            newValue: {'url': 'https://example.com/pr/7', 'label': 'PR 7'},
            newName: 'PR 7',
          ),
        ),
        'Added URL link PR 7',
      );
      expect(
        _change(
          _entryJson(
            field: 'urlLink',
            oldValue: {'url': 'https://example.com/pr/7', 'label': null},
          ),
        ),
        'Removed URL link https://example.com/pr/7',
      );
    });

    test('attachments read as added or removed, by file name', () {
      expect(
        _change(
          _entryJson(
            field: 'attachment',
            newValue: {'id': 'att-1', 'fileName': 'report.pdf'},
            newName: 'report.pdf',
          ),
        ),
        'Added attachment report.pdf',
      );
      expect(
        _change(
          _entryJson(
            field: 'attachment',
            oldValue: {'id': 'att-1', 'fileName': 'old quote.pdf'},
          ),
        ),
        'Removed attachment old quote.pdf',
      );
    });

    test('untyped links from before link types name the other item', () {
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

    test('a custom field goes by its name', () {
      expect(
        _change(
          _entryJson(
            field: 'fields.f-1',
            fieldName: 'Budget',
            oldValue: 1200,
            newValue: 900,
          ),
        ),
        'Budget: 1200 → 900',
      );
    });

    test('a custom field since removed says so', () {
      expect(
        _change(_entryJson(field: 'fields.f-1', oldValue: 'Oak')),
        'Removed field: Oak → none',
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
