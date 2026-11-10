import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/data/work_item_history_repository.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_attachments_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';
import 'package:weaver/features/work_item_detail/work_item_history_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/roll_up.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';
import 'package:weaver/shared/widgets/user_name_label.dart';

import '../../shared/fakes/work_items.dart';
import '../../shared/recurrence/fake_recurrence_repository.dart';
import 'fakes/fake_attachment_files.dart';
import 'fakes/fake_attachment_repository.dart';

class _NoUser implements CurrentUser {
  @override
  String? get currentUserId => null;
}

/// Serves what the detail view loads and saves descriptions, statuses,
/// parents and sub-items; anything else is unexpected.
class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.description, this.parentId});

  String? description;
  String? parentId;
  String statusId = 'todo';
  int version = 1;
  DateTime? completionDate;
  final List<(String, int)> statusSaves = [];
  final List<(String?, int)> parentSaves = [];
  final List<String> searches = [];
  final List<String> createdSubItems = [];
  final List<WorkItemChildSummary> children = [];
  final List<RollUp> rollUps = [];
  final List<String> allSpacesSearches = [];
  final List<(String, WorkItemLinkRelation)> linkSaves = [];
  final List<(String, String?)> urlLinkSaves = [];

  /// When set, the server refuses the next link with this reason.
  String? linkRefusal;

  /// Items other than this one, which [getItem] and the search serve.
  final Map<String, WorkItem> others = {};

  /// Ids the server refuses as a parent, as this item's own descendants.
  Set<String> descendantIds = const {};

  /// When set, the next description save loses to this description, saved
  /// by someone else, and fails with a 409.
  String? theirDescription;
  final List<(String, int)> descriptionSaves = [];

  @override
  Future<WorkItem> getItem(String id) async =>
      others[id] ??
      WorkItem(
        number: 1,
        id: id,
        spaceId: 'home',
        parentId: parentId,
        title: 'Original title',
        description: description,
        statusId: statusId,
        typeId: null,
        priorityId: null,
        assignedToUserId: null,
        startDate: null,
        endDate: null,
        createdAt: DateTime(2026, 9, 1),
        updatedAt: DateTime(2026, 9, 1),
        version: version,
        completionDate: completionDate,
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
  Future<WorkItem> changeStatus(
    String id,
    String statusId, {
    required int expectedVersion,
  }) {
    statusSaves.add((statusId, expectedVersion));
    this.statusId = statusId;
    version++;
    return getItem(id);
  }

  @override
  Future<WorkItem> reparent(
    String id,
    String? parentId, {
    required int expectedVersion,
  }) {
    parentSaves.add((parentId, expectedVersion));
    if (descendantIds.contains(parentId)) {
      return Future.error(
        const ApiException('it would create a cycle', statusCode: 409),
      );
    }
    this.parentId = parentId;
    version++;
    return getItem(id);
  }

  @override
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) async {
    searches.add(text);
    return [
      for (final item in others.values)
        if (item.title.toLowerCase().contains(text.toLowerCase())) item,
    ];
  }

  @override
  Future<WorkItemChildSummary> createSubItem(
    String parentId, {
    required String title,
    required String statusId,
  }) async {
    createdSubItems.add(title);
    final child = WorkItemChildSummary(
      id: 'child-${children.length + 1}',
      number: 40 + children.length,
      title: title,
      statusId: statusId,
    );
    children.add(child);
    return child;
  }

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) async => const [
    WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
    WorkItemStatus(id: 'done', name: 'Done', order: 1),
  ];

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) async => const [];

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
  Future<List<WorkItemUrlLink>> loadUrlLinks(String workItemId) async =>
      const [];

  @override
  Future<List<WorkItem>> searchAllWorkItems(String text) async {
    allSpacesSearches.add(text);
    return [
      for (final item in others.values)
        if (item.title.toLowerCase().contains(text.toLowerCase())) item,
    ];
  }

  @override
  Future<WorkItemLink> addLink(
    String workItemId,
    String targetWorkItemId,
    WorkItemLinkRelation type,
  ) async {
    linkSaves.add((targetWorkItemId, type));
    if (linkRefusal case final reason?) {
      throw ApiException(reason, statusCode: 409);
    }
    final target = others[targetWorkItemId]!;
    return WorkItemLink(
      id: 'link-${linkSaves.length}',
      type: type,
      linkedWorkItemId: target.id,
      linkedWorkItemNumber: target.number,
      linkedWorkItemTitle: target.title,
      linkedWorkItemSpaceId: target.spaceId,
    );
  }

  @override
  Future<WorkItemUrlLink> addUrlLink(
    String workItemId,
    String url, {
    String? label,
  }) async {
    urlLinkSaves.add((url, label));
    return WorkItemUrlLink(
      id: 'url-${urlLinkSaves.length}',
      url: url,
      label: label,
    );
  }

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async =>
      List.of(children);

  @override
  Future<List<RollUp>> loadRollUps(String id) async => List.of(rollUps);

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
  FakeAttachmentRepository? attachments,
  FakeAttachmentFiles? files,
  VoidCallback? onChanged,
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
                onChanged: onChanged,
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
                attachmentsViewModel: WorkItemAttachmentsViewModel(
                  attachments ?? FakeAttachmentRepository(),
                  files ?? FakeAttachmentFiles(),
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
  testWidgets("shows sub-items' totals of fields its Type doesn't have", (
    tester,
  ) async {
    final repository = _FakeRepository()
      ..rollUps.add(
        const RollUp(
          fieldId: 'field-hours',
          fieldName: 'Hours',
          typeId: 'type-task',
          kind: CustomFieldKind.number,
          total: 7.5,
        ),
      );

    await _openDetails(tester, repository: repository);

    expect(find.text('Totals of sub-items'), findsOneWidget);
    expect(find.text('Hours: 7.5'), findsOneWidget);
  });

  testWidgets('a Done item shows the day it was completed', (tester) async {
    final repository = _FakeRepository()
      ..statusId = 'done'
      ..completionDate = DateTime(2026, 9, 3);

    await _openDetails(tester, repository: repository);

    expect(
      find.text(
        'Created 2026-09-01 · Updated 2026-09-01 · Completed 2026-09-03',
      ),
      findsOneWidget,
    );
  });

  testWidgets('an item that is not Done shows no completion date', (
    tester,
  ) async {
    await _openDetails(tester);

    expect(
      find.text('Created 2026-09-01 · Updated 2026-09-01'),
      findsOneWidget,
    );
  });

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

  group('status', () {
    testWidgets("offers the Space's statuses and saves the pick", (
      tester,
    ) async {
      final repository = _FakeRepository();
      var changed = false;
      await _openDetails(
        tester,
        repository: repository,
        onChanged: () => changed = true,
      );

      await tester.tap(find.byKey(const Key('status-picker')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Done').last);
      await tester.pumpAndSettle();

      expect(repository.statusSaves, [('done', 1)]);
      expect(
        find.descendant(
          of: find.byKey(const Key('status-picker')),
          matching: find.text('Done'),
        ),
        findsOneWidget,
      );
      expect(changed, isTrue, reason: 'the board refreshes on close');
    });
  });

  group('parent', () {
    WorkItem kitchen() => workItem('kitchen', number: 5, title: 'Kitchen');
    WorkItem garden() => workItem('garden', number: 9, title: 'Garden');

    Finder parentField(String text) => find.descendant(
      of: find.byKey(const Key('parent-picker')),
      matching: find.text(text),
    );

    Future<void> pickParent(WidgetTester tester, String option) async {
      await tester.tap(find.byKey(const Key('parent-picker')));
      await tester.pumpAndSettle();
      await tester.tap(find.text(option));
      await tester.pumpAndSettle();
    }

    testWidgets("shows the parent's number and title", (tester) async {
      final repository = _FakeRepository(parentId: 'kitchen')
        ..others['kitchen'] = kitchen();
      await _openDetails(tester, repository: repository);

      expect(parentField('#5 Kitchen'), findsOneWidget);
    });

    testWidgets('searches the Space and moves the item under the pick', (
      tester,
    ) async {
      final repository = _FakeRepository()
        ..others['kitchen'] = kitchen()
        ..others['garden'] = garden();
      var changed = false;
      await _openDetails(
        tester,
        repository: repository,
        onChanged: () => changed = true,
      );
      expect(parentField('None'), findsOneWidget);

      await tester.tap(find.byKey(const Key('parent-picker')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextField, 'Search by title or #number'),
        'gar',
      );
      await tester.pumpAndSettle(const Duration(milliseconds: 400));
      expect(find.text('#5 Kitchen'), findsNothing);
      await tester.tap(find.text('#9 Garden'));
      await tester.pumpAndSettle();

      expect(repository.searches, ['', 'gar']);
      expect(repository.parentSaves, [('garden', 1)]);
      expect(parentField('#9 Garden'), findsOneWidget);
      expect(changed, isTrue, reason: 'the board refreshes on close');
    });

    testWidgets('"No parent" makes the item top-level', (tester) async {
      final repository = _FakeRepository(parentId: 'kitchen')
        ..others['kitchen'] = kitchen();
      await _openDetails(tester, repository: repository);

      await pickParent(tester, 'No parent');

      expect(repository.parentSaves, [(null, 1)]);
      expect(parentField('None'), findsOneWidget);
    });

    testWidgets('a parent inside the item is refused with a clear message', (
      tester,
    ) async {
      final repository = _FakeRepository(parentId: 'kitchen')
        ..others['kitchen'] = kitchen()
        ..others['garden'] = garden()
        ..descendantIds = {'garden'};
      await _openDetails(tester, repository: repository);

      await pickParent(tester, '#9 Garden');

      expect(
        find.text(
          "#9 Garden is inside this work item, so it can't be its parent.",
        ),
        findsOneWidget,
      );
      expect(parentField('#5 Kitchen'), findsOneWidget);
    });
  });

  testWidgets('adding a sub-item lists it under Sub-Items', (tester) async {
    final repository = _FakeRepository();
    var changed = false;
    await _openDetails(
      tester,
      repository: repository,
      onChanged: () => changed = true,
    );
    expect(find.text('No sub-items'), findsOneWidget);

    await tester.enterText(
      find.widgetWithText(TextField, 'New sub-item title'),
      'Paint the walls',
    );
    await tester.tap(find.widgetWithText(TextButton, 'Add sub-item'));
    await tester.pumpAndSettle();

    expect(repository.createdSubItems, ['Paint the walls']);
    expect(find.text('#40 Paint the walls'), findsOneWidget);
    expect(find.text('No sub-items'), findsNothing);
    expect(changed, isTrue, reason: 'the board refreshes on close');
  });

  group('links', () {
    WorkItem paint() =>
        workItem('paint', number: 12, title: 'Paint', spaceId: 'house');

    Future<void> addLink(
      WorkItemLinkRelation type,
      String pick,
      WidgetTester tester,
    ) async {
      await tester.ensureVisible(find.byKey(const Key('link-type-picker')));
      await tester.tap(find.byKey(const Key('link-type-picker')));
      await tester.pumpAndSettle();
      await tester.tap(find.text(type.label).last);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'Add link'));
      await tester.pumpAndSettle();
      await tester.tap(find.text(pick));
      await tester.pumpAndSettle();
    }

    testWidgets('picks an item from any Space and lists it under its type', (
      tester,
    ) async {
      final repository = _FakeRepository()..others['paint'] = paint();
      await _openDetails(tester, repository: repository);

      await addLink(WorkItemLinkRelation.blocks, '#12 Paint', tester);

      expect(repository.allSpacesSearches, ['']);
      expect(repository.searches, isEmpty);
      expect(repository.linkSaves, [('paint', WorkItemLinkRelation.blocks)]);
      expect(find.text('No linked work items'), findsNothing);
      expect(find.text('#12 Paint'), findsOneWidget);
    });

    testWidgets("a refused link shows the server's reason", (tester) async {
      final repository = _FakeRepository()
        ..others['paint'] = paint()
        ..linkRefusal = 'Paint already blocks this item (409).';
      await _openDetails(tester, repository: repository);

      await addLink(WorkItemLinkRelation.blocks, '#12 Paint', tester);

      expect(
        find.text('Paint already blocks this item (409).'),
        findsOneWidget,
      );
      expect(find.text('No linked work items'), findsOneWidget);
    });

    testWidgets('a URL link that is not a full address is refused before '
        'saving', (tester) async {
      final repository = _FakeRepository();
      await _openDetails(tester, repository: repository);

      await tester.enterText(
        find.widgetWithText(TextField, 'https://…'),
        'example.com',
      );
      await tester.ensureVisible(
        find.widgetWithText(TextButton, 'Add URL link'),
      );
      await tester.tap(find.widgetWithText(TextButton, 'Add URL link'));
      await tester.pumpAndSettle();

      expect(repository.urlLinkSaves, isEmpty);
      expect(
        find.text('Enter a full web address starting with https://.'),
        findsOneWidget,
      );
    });

    testWidgets('adding a URL link lists it by its label', (tester) async {
      final repository = _FakeRepository();
      await _openDetails(tester, repository: repository);

      await tester.enterText(
        find.widgetWithText(TextField, 'https://…'),
        'https://example.com/pr/7',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Label (optional)'),
        'PR 7',
      );
      await tester.ensureVisible(
        find.widgetWithText(TextButton, 'Add URL link'),
      );
      await tester.tap(find.widgetWithText(TextButton, 'Add URL link'));
      await tester.pumpAndSettle();

      expect(repository.urlLinkSaves, [('https://example.com/pr/7', 'PR 7')]);
      expect(find.text('PR 7'), findsOneWidget);
    });
  });

  group('attachments', () {
    testWidgets("lists the item's attachments", (tester) async {
      await _openDetails(
        tester,
        attachments: FakeAttachmentRepository([attachment(sizeBytes: 2048)]),
      );

      expect(find.text('report.pdf'), findsOneWidget);
      expect(find.text('2 KB'), findsOneWidget);
    });

    testWidgets('attaching a file uploads it and lists it', (tester) async {
      final attachments = FakeAttachmentRepository();
      await _openDetails(
        tester,
        attachments: attachments,
        files: FakeAttachmentFiles(
          picked: const AttachmentUpload(
            fileName: 'quote.pdf',
            bytes: [1, 2, 3],
            contentType: 'application/pdf',
          ),
        ),
      );

      final attach = find.widgetWithText(TextButton, 'Attach file');
      await tester.ensureVisible(attach);
      await tester.tap(attach);
      await tester.pumpAndSettle();

      expect(attachments.uploads.map((u) => u.fileName), ['quote.pdf']);
      expect(find.text('quote.pdf'), findsOneWidget);
    });

    testWidgets('a confirmed delete removes the attachment', (tester) async {
      final attachments = FakeAttachmentRepository([attachment()]);
      await _openDetails(tester, attachments: attachments);

      final delete = find.byTooltip('Delete attachment');
      await tester.ensureVisible(delete);
      await tester.tap(delete);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
      await tester.pumpAndSettle();

      expect(attachments.deleted, ['att-1']);
      expect(find.text('report.pdf'), findsNothing);
    });
  });
}
