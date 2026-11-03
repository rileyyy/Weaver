import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/data/work_item_history_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_history_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_layer.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/widgets/user_name_label.dart';

import '../../shared/recurrence/fake_recurrence_repository.dart';

class _NoUser implements CurrentUser {
  @override
  String? get currentUserId => null;
}

/// Serves what the detail view loads and saves descriptions; anything else
/// is unexpected.
class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.description});

  String? description;
  int version = 1;

  /// When set, the next description save loses to this description, saved
  /// by someone else, and fails with a 409.
  String? theirDescription;
  final List<(String, int)> descriptionSaves = [];

  @override
  Future<WorkItem> getItem(String id) async => WorkItem(
    number: 1,
    id: id,
    spaceId: 'home',
    parentId: null,
    title: 'Original title',
    description: description,
    statusId: 'todo',
    layerId: null,
    priorityId: null,
    assignedToUserId: null,
    startDate: null,
    endDate: null,
    createdAt: DateTime(2026, 9, 1),
    updatedAt: DateTime(2026, 9, 1),
    version: version,
  );

  @override
  Future<WorkItem> updateDescription(
    String id,
    String description, {
    required int expectedVersion,
  }) {
    descriptionSaves.add((description, expectedVersion));
    version++;
    if (theirDescription case final theirs?) {
      this.description = theirs;
      return Future.error(const ApiException('changed', statusCode: 409));
    }
    this.description = description;
    return getItem(id);
  }

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) async => const [
    WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
  ];

  @override
  Future<List<WorkItemLayer>> loadLayers() async => const [];

  @override
  Future<List<WorkItemPriority>> loadPriorities(String spaceId) async =>
      const [];

  @override
  Future<List<User>> loadUsers() async => const [
    User(id: 'user-1', username: 'alice', kind: UserKind.human),
    User(id: 'agent-1', username: 'Claude Code', kind: UserKind.agent),
  ];

  @override
  Future<List<WorkItemComment>> loadComments(String workItemId) async =>
      const [];

  @override
  Future<List<WorkItemLink>> loadLinks(String workItemId) async => const [];

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async =>
      const [];

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError('${invocation.memberName}');
}

class _FakeHistoryRepository implements WorkItemHistoryRepository {
  _FakeHistoryRepository(this.entries);

  final List<WorkItemHistoryEntry> entries;
  final List<String> loads = [];

  @override
  Future<List<WorkItemHistoryEntry>> loadHistory(String workItemId) async {
    loads.add(workItemId);
    return entries;
  }
}

WorkItemHistoryEntry _historyEntry({
  required int id,
  required String field,
  Object? oldValue,
  Object? newValue,
  String accountName = 'alice',
  UserKind accountKind = UserKind.human,
  String? actorLabel,
}) => WorkItemHistoryEntry(
  id: id,
  changedAt: DateTime(2026, 9, 2, 14, 30),
  accountName: accountName,
  accountKind: accountKind,
  apiKeyLabel: null,
  actorLabel: actorLabel,
  field: field,
  oldValue: oldValue,
  newValue: newValue,
  oldName: null,
  newName: null,
);

Future<void> _openDetails(
  WidgetTester tester, {
  FakeRecurrenceRepository? recurrences,
  WorkItemHistoryRepository? history,
  _FakeRepository? repository,
}) async {
  tester.view.physicalSize = const Size(1200, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) => WorkItemDetailView(
                workItemId: 'item-1',
                viewModel: WorkItemDetailViewModel(
                  repository ?? _FakeRepository(),
                  _NoUser(),
                  WorkItemStore(),
                ),
                repeatViewModel: RepeatSettingsViewModel(
                  recurrences ?? FakeRecurrenceRepository(),
                ),
                historyViewModel: WorkItemHistoryViewModel(
                  history ?? _FakeHistoryRepository(const []),
                ),
              ),
            ),
          ),
          child: const Text('open'),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

Finder get _titleField => find.widgetWithText(TextField, 'Original title');

void main() {
  testWidgets('closing without edits needs no confirmation', (tester) async {
    await _openDetails(tester);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('Work Item Details'), findsNothing);
  });

  testWidgets('closing with an unsaved title asks before discarding it', (
    tester,
  ) async {
    await _openDetails(tester);
    await tester.enterText(_titleField, 'Edited title');
    await tester.pump();
    expect(find.text('Unsaved changes'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('Discard unsaved changes?'), findsOneWidget);

    await tester.tap(find.text('Keep editing'));
    await tester.pumpAndSettle();
    expect(find.text('Edited title'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tester.tap(find.text('Discard'));
    await tester.pumpAndSettle();
    expect(find.text('Work Item Details'), findsNothing);
  });

  testWidgets('a blank title is flagged and cannot be saved', (tester) async {
    await _openDetails(tester);

    await tester.enterText(_titleField, '');
    await tester.pump();

    expect(find.text('Title is required.'), findsOneWidget);
    final save = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Save details'),
    );
    expect(save.onPressed, isNull);
  });

  group('description', () {
    Finder descriptionField(String text) =>
        find.widgetWithText(TextField, text);

    testWidgets('shows rendered Markdown, and the raw text when editing', (
      tester,
    ) async {
      await _openDetails(
        tester,
        repository: _FakeRepository(description: 'Buy **fresh** bread'),
      );

      expect(find.textContaining('**', findRichText: true), findsNothing);
      expect(
        find.textContaining('fresh bread', findRichText: true),
        findsOneWidget,
      );

      await tester.tap(find.byTooltip('Edit description'));
      await tester.pump();
      expect(descriptionField('Buy **fresh** bread'), findsOneWidget);

      await tester.enterText(descriptionField('Buy **fresh** bread'), '# Oat');
      await tester.tap(find.byTooltip('Preview description'));
      await tester.pump();
      expect(descriptionField('# Oat'), findsNothing);
      expect(find.text('Oat', findRichText: true), findsOneWidget);
      expect(find.text('Unsaved changes'), findsOneWidget);
    });

    testWidgets('ticking a box saves that line with the item version', (
      tester,
    ) async {
      final repository = _FakeRepository(description: '- [ ] milk\n- [ ] eggs');
      await _openDetails(tester, repository: repository);

      await tester.tap(find.byType(Checkbox).at(1));
      await tester.pumpAndSettle();

      expect(repository.descriptionSaves, [('- [ ] milk\n- [x] eggs', 1)]);
      final boxes = tester.widgetList<Checkbox>(find.byType(Checkbox));
      expect(boxes.map((box) => box.value), [false, true]);
      expect(find.text('Unsaved changes'), findsNothing);

      await tester.tap(find.byType(Checkbox).at(0));
      await tester.pumpAndSettle();
      expect(repository.descriptionSaves.last, ('- [x] milk\n- [x] eggs', 2));
    });

    testWidgets('a tick that conflicts reloads the item and says so', (
      tester,
    ) async {
      final repository = _FakeRepository(description: '- [ ] milk')
        ..theirDescription = '- [ ] milk\n- [ ] eggs';
      await _openDetails(tester, repository: repository);

      await tester.tap(find.byType(Checkbox));
      await tester.pumpAndSettle();

      expect(
        find.textContaining('Someone else changed this work item'),
        findsOneWidget,
      );
      final boxes = tester.widgetList<Checkbox>(find.byType(Checkbox));
      expect(boxes.map((box) => box.value), [false, false]);
      expect(find.textContaining('eggs', findRichText: true), findsOneWidget);
    });

    testWidgets('boxes are read-only while the description has unsaved edits', (
      tester,
    ) async {
      await _openDetails(
        tester,
        repository: _FakeRepository(description: '- [ ] milk'),
      );

      await tester.tap(find.byTooltip('Edit description'));
      await tester.pump();
      await tester.enterText(descriptionField('- [ ] milk'), '- [ ] oat milk');
      await tester.tap(find.byTooltip('Preview description'));
      await tester.pump();

      final box = tester.widget<Checkbox>(find.byType(Checkbox));
      expect(box.onChanged, isNull);
    });
  });

  testWidgets('setting up a repeat is saved with Save repeat', (tester) async {
    final recurrences = FakeRecurrenceRepository();
    await _openDetails(tester, recurrences: recurrences);

    await tester.ensureVisible(find.text('Repeat this item'));
    await tester.tap(find.text('Repeat this item'));
    await tester.pumpAndSettle();
    expect(find.byType(Checkbox), findsNWidgets(7));

    await tester.ensureVisible(find.text('Save repeat'));
    await tester.tap(find.text('Save repeat'));
    await tester.pumpAndSettle();

    expect(recurrences.saveCalls.single.$1, 'item-1');
    expect(find.text('Stop repeating'), findsOneWidget);
  });

  testWidgets('closing with an unsaved repeat asks before discarding it', (
    tester,
  ) async {
    await _openDetails(tester);
    await tester.ensureVisible(find.text('Repeat this item'));
    await tester.tap(find.text('Repeat this item'));
    await tester.pumpAndSettle();

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('Discard unsaved changes?'), findsOneWidget);
  });

  testWidgets('stopping a repeat asks first', (tester) async {
    final recurrences = FakeRecurrenceRepository([recurrence()]);
    await _openDetails(tester, recurrences: recurrences);

    await tester.ensureVisible(find.text('Stop repeating'));
    await tester.tap(find.text('Stop repeating'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Stop repeating'));
    await tester.pumpAndSettle();

    expect(recurrences.removeCalls, ['item-1']);
    expect(find.text('Repeat this item'), findsOneWidget);
  });

  testWidgets('the History tab lists changes newest first, marking agents', (
    tester,
  ) async {
    final history = _FakeHistoryRepository([
      _historyEntry(
        id: 2,
        field: 'title',
        oldValue: 'Original title',
        newValue: 'Renamed',
        accountName: 'Claude Code',
        accountKind: UserKind.agent,
        actorLabel: 'sub-agent: test-writer',
      ),
      _historyEntry(id: 1, field: 'created', newValue: 'Original title'),
    ]);
    await _openDetails(tester, history: history);
    expect(history.loads, isEmpty);

    await tester.tap(find.text('History'));
    await tester.pumpAndSettle();

    expect(history.loads, ['item-1']);
    final rename = find.text('Title: Original title → Renamed');
    final created = find.text('Created "Original title"');
    expect(rename, findsOneWidget);
    expect(created, findsOneWidget);
    expect(
      tester.getTopLeft(rename).dy,
      lessThan(tester.getTopLeft(created).dy),
    );
    expect(find.text('Agent'), findsOneWidget);
    expect(find.text('sub-agent: test-writer'), findsOneWidget);
    expect(find.text('alice'), findsOneWidget);
  });

  testWidgets('an empty history says so', (tester) async {
    await _openDetails(tester);

    await tester.tap(find.text('History'));
    await tester.pumpAndSettle();

    expect(find.text('No changes recorded yet.'), findsOneWidget);
  });
  testWidgets('the assignee picker marks agent accounts', (tester) async {
    await _openDetails(tester);

    await tester.tap(find.text('Unassigned'));
    await tester.pumpAndSettle();

    expect(
      find.descendant(
        of: find.widgetWithText(UserNameLabel, 'Claude Code').last,
        matching: find.text('Agent'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: find.widgetWithText(UserNameLabel, 'alice').last,
        matching: find.text('Agent'),
      ),
      findsNothing,
    );
  });
}
