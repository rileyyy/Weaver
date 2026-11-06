import 'dart:async';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick_dates.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

import '../../shared/fakes/work_items.dart';

WorkItem _item({
  String id = 'item-1',
  int number = 1,
  String? parentId,
  String statusId = 'todo',
  String? typeId,
  String? priorityId,
  String? assignedToUserId,
  List<String> tags = const [],
  String title = 'A task',
  String? description,
  String? when,
  bool whenResolved = false,
  Map<String, Object?> fields = const {},
  int version = 1,
}) => WorkItem(
  number: number,
  id: id,
  spaceId: 'home',
  parentId: parentId,
  title: title,
  description: description,
  statusId: statusId,
  typeId: typeId,
  priorityId: priorityId,
  assignedToUserId: assignedToUserId,
  startDate: null,
  endDate: null,
  createdAt: DateTime.utc(2026, 1, 1),
  updatedAt: DateTime.utc(2026, 1, 1),
  version: version,
  tags: tags,
  fields: fields,
  when: when,
  whenResolved: whenResolved,
);

class _FakeCurrentUser implements CurrentUser {
  const _FakeCurrentUser(this.currentUserId);

  @override
  final String? currentUserId;
}

const _currentUser = _FakeCurrentUser('user-1');

class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.getItemError, this.saveError});

  Object? getItemError;
  final Exception? saveError;
  WorkItem item = _item();
  bool deleted = false;
  final List<String> updateDetailsCalls = [];
  final List<String> updateDescriptionCalls = [];
  final List<String?> assignCalls = [];
  final List<String> rescheduleCalls = [];
  final List<(DateTime?, DateTime?)> rescheduledDates = [];
  final List<String?> updateWhenCalls = [];
  List<QuickPickDates> quickPicks = const [];
  final List<List<String>> updateTagsCalls = [];
  final List<int> expectedVersions = [];
  final List<WorkItemComment> commentsList = [];
  final List<WorkItemLink> linksList = [];
  final List<(String, WorkItemLinkRelation)> addLinkCalls = [];
  final List<WorkItemUrlLink> urlLinksList = [];
  final List<(String, String?)> addUrlLinkCalls = [];
  final List<String> allSpacesSearchCalls = [];
  List<WorkItemChildSummary> childrenList = [];
  int loadCommentsCalls = 0;
  int getItemCalls = 0;
  int loadLinksCalls = 0;
  final List<String> lookupSpaceIds = [];
  Map<String, List<CustomField>> fieldsByType = {};
  final List<String> loadCustomFieldsCalls = [];
  final List<Map<String, Object?>> setCustomFieldsCalls = [];
  final List<String> changeStatusCalls = [];
  final List<String?> reparentCalls = [];
  final List<(String, String, String)> createSubItemCalls = [];
  final List<(String, String)> searchCalls = [];
  List<WorkItem> searchResults = const [];

  /// Ids the server would refuse as this item's parent, since they're the
  /// item's own descendants.
  Set<String> descendantIds = const {};

  /// Items [getItem] serves other than [item], such as its parent.
  final Map<String, WorkItem> others = {};

  /// When set, [assign] waits on it, so a test can overlap two saves.
  Completer<void>? assignGate;

  /// Rejects a write based on a stale version, as the server does.
  void _checkVersion(int expectedVersion) {
    if (expectedVersion != item.version) {
      throw const ApiException('Changed by someone else', statusCode: 409);
    }
  }

  @override
  Future<WorkItem> getItem(String id) async {
    getItemCalls++;
    final error = getItemError;
    // An Object so tests can simulate either an API failure or a bug.
    if (error != null) Error.throwWithStackTrace(error, StackTrace.current);
    return others[id] ?? item;
  }

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async =>
      List.of(childrenList);

  @override
  Future<void> deleteItem(
    String id, {
    bool cascade = false,
    required int expectedVersion,
  }) async {
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    deleted = true;
  }

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) async {
    lookupSpaceIds.add(spaceId);
    return const [
      WorkItemStatus(
        id: 'todo',
        name: 'To Do',
        order: 0,
        color: Color(0xFF00FF00),
      ),
      WorkItemStatus(id: 'done', name: 'Done', order: 1),
    ];
  }

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) async {
    lookupSpaceIds.add(spaceId);
    return const [WorkItemType(id: 'type-task', name: 'Task', order: 0)];
  }

  @override
  Future<List<WorkItemPriority>> loadPriorities(String spaceId) async {
    lookupSpaceIds.add(spaceId);
    return const [
      WorkItemPriority(id: 'priority-high', name: 'High', order: 2),
    ];
  }

  @override
  Future<List<User>> loadUsers() async => const [
    User(id: 'user-1', username: 'alice', kind: UserKind.human),
  ];

  @override
  Future<WorkItem> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? typeId,
    required String? priorityId,
    required int expectedVersion,
  }) async {
    updateDetailsCalls.add(id);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = _item(
      id: id,
      statusId: item.statusId,
      typeId: typeId,
      priorityId: priorityId,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItem> updateDescription(
    String id,
    String description, {
    required int expectedVersion,
  }) async {
    updateDescriptionCalls.add(description);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = _item(
      id: id,
      statusId: item.statusId,
      title: item.title,
      description: description,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItem> assign(
    String id,
    String? userId, {
    required int expectedVersion,
  }) async {
    assignCalls.add(userId);
    expectedVersions.add(expectedVersion);
    await assignGate?.future;
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = _item(
      id: id,
      statusId: item.statusId,
      assignedToUserId: userId,
      tags: item.tags,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItem> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) async {
    rescheduleCalls.add(id);
    rescheduledDates.add((startDate, endDate));
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = withVersion(item, item.version + 1);
  }

  @override
  Future<WorkItem> changeStatus(
    String id,
    String statusId, {
    required int expectedVersion,
  }) async {
    changeStatusCalls.add(statusId);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = _item(
      id: id,
      statusId: statusId,
      parentId: item.parentId,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItem> reparent(
    String id,
    String? parentId, {
    required int expectedVersion,
  }) async {
    reparentCalls.add(parentId);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    if (parentId != null && descendantIds.contains(parentId)) {
      throw const ApiException(
        'Cannot set parent: it would create a cycle.',
        statusCode: 409,
      );
    }
    return item = _item(
      id: id,
      statusId: item.statusId,
      parentId: parentId,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItemChildSummary> createSubItem(
    String parentId, {
    required String title,
    required String statusId,
  }) async {
    createSubItemCalls.add((parentId, title, statusId));
    final error = saveError;
    if (error != null) throw error;
    final child = WorkItemChildSummary(
      id: 'child-${childrenList.length + 1}',
      number: 100 + childrenList.length,
      title: title,
      statusId: statusId,
    );
    childrenList.add(child);
    return child;
  }

  @override
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) async {
    searchCalls.add((spaceId, text));
    final error = saveError;
    if (error != null) throw error;
    return searchResults;
  }

  @override
  Future<WorkItem> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  }) async {
    updateTagsCalls.add(tags);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    return item = _item(
      id: id,
      statusId: item.statusId,
      assignedToUserId: item.assignedToUserId,
      tags: tags,
      version: item.version + 1,
    );
  }

  @override
  Future<List<WorkItemComment>> loadComments(String workItemId) async {
    loadCommentsCalls++;
    return List.of(commentsList);
  }

  @override
  Future<WorkItemComment> addComment(String workItemId, String body) async {
    final error = saveError;
    if (error != null) throw error;
    final comment = WorkItemComment(
      id: 'comment-${commentsList.length + 1}',
      workItemId: workItemId,
      authorUserId: 'user-1',
      authorUsername: 'alice',
      body: body,
      createdAt: DateTime.utc(2026, 1, 1),
      updatedAt: null,
    );
    commentsList.add(comment);
    return comment;
  }

  @override
  Future<WorkItemComment> updateComment(String commentId, String body) async {
    final error = saveError;
    if (error != null) throw error;
    final index = commentsList.indexWhere((c) => c.id == commentId);
    final updated = WorkItemComment(
      id: commentId,
      workItemId: commentsList[index].workItemId,
      authorUserId: commentsList[index].authorUserId,
      authorUsername: commentsList[index].authorUsername,
      body: body,
      createdAt: commentsList[index].createdAt,
      updatedAt: DateTime.utc(2026, 1, 2),
    );
    commentsList[index] = updated;
    return updated;
  }

  @override
  Future<void> deleteComment(String commentId) async {
    final error = saveError;
    if (error != null) throw error;
    commentsList.removeWhere((c) => c.id == commentId);
  }

  @override
  Future<List<WorkItemLink>> loadLinks(String workItemId) async =>
      List.of(linksList);

  @override
  Future<WorkItemLink> addLink(
    String workItemId,
    String targetWorkItemId,
    WorkItemLinkRelation type,
  ) async {
    addLinkCalls.add((targetWorkItemId, type));
    final error = saveError;
    if (error != null) throw error;
    final link = WorkItemLink(
      id: 'link-${linksList.length + 1}',
      type: type,
      linkedWorkItemId: targetWorkItemId,
      linkedWorkItemNumber: 2,
      linkedWorkItemTitle: 'Target $targetWorkItemId',
      linkedWorkItemSpaceId: 'work',
    );
    linksList.add(link);
    return link;
  }

  @override
  Future<List<WorkItemUrlLink>> loadUrlLinks(String workItemId) async =>
      List.of(urlLinksList);

  @override
  Future<WorkItemUrlLink> addUrlLink(
    String workItemId,
    String url, {
    String? label,
  }) async {
    addUrlLinkCalls.add((url, label));
    final error = saveError;
    if (error != null) throw error;
    final link = WorkItemUrlLink(
      id: 'url-${urlLinksList.length + 1}',
      url: url,
      label: label,
    );
    urlLinksList.add(link);
    return link;
  }

  @override
  Future<void> deleteUrlLink(String urlLinkId) async {
    final error = saveError;
    if (error != null) throw error;
    urlLinksList.removeWhere((l) => l.id == urlLinkId);
  }

  @override
  Future<List<WorkItem>> searchAllWorkItems(String text) async {
    allSpacesSearchCalls.add(text);
    final error = saveError;
    if (error != null) throw error;
    return searchResults;
  }

  @override
  Future<void> deleteLink(String linkId) async {
    final error = saveError;
    if (error != null) throw error;
    linksList.removeWhere((l) => l.id == linkId);
  }

  @override
  Future<WorkItem> updateWhen(
    String id,
    String? when, {
    required int expectedVersion,
  }) async {
    updateWhenCalls.add(when);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    return item = _item(id: id, when: when, version: item.version + 1);
  }

  @override
  Future<List<QuickPickDates>> loadQuickPicks() async => quickPicks;

  @override
  Future<List<CustomField>> loadCustomFields(String typeId) async {
    loadCustomFieldsCalls.add(typeId);
    return fieldsByType[typeId] ?? const [];
  }

  @override
  Future<WorkItem> setCustomFields(
    String id,
    Map<String, Object?> values, {
    required int expectedVersion,
  }) async {
    setCustomFieldsCalls.add(values);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    _checkVersion(expectedVersion);
    final fields = {...item.fields, ...values}
      ..removeWhere((_, value) => value == null);
    return item = _item(
      id: id,
      statusId: item.statusId,
      typeId: item.typeId,
      fields: fields,
      version: item.version + 1,
    );
  }
}

const _budget = CustomField(
  id: 'field-budget',
  typeId: 'type-room',
  name: 'Budget',
  kind: CustomFieldKind.currency,
  order: 0,
);

const _effort = CustomField(
  id: 'field-effort',
  typeId: 'type-task',
  name: 'Effort',
  kind: CustomFieldKind.number,
  order: 0,
);

void main() {
  test('load populates item, statuses, types, priorities and users', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );

    await viewModel.load('item-1');

    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
    expect(viewModel.item!.id, 'item-1');
    expect(viewModel.statuses, hasLength(2));
    expect(viewModel.types, hasLength(1));
    expect(viewModel.priorities.single.name, 'High');
    expect(viewModel.users, hasLength(1));
  });

  test(
    "load offers the statuses, Types and priorities of the item's own Space",
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );

      await viewModel.load('item-1');

      expect(repository.lookupSpaceIds, ['home', 'home', 'home']);
    },
  );

  test('load sets loadError when the repository throws', () async {
    final repository = _FakeRepository(
      getItemError: const ApiException('network down'),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );

    await viewModel.load('item-1');

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.isLoading, isFalse);
  });

  test('load populates children from the repository', () async {
    final repository = _FakeRepository()
      ..childrenList = [
        const WorkItemChildSummary(
          id: 'child-1',
          number: 7,
          title: 'A sub-item',
          statusId: 'todo',
        ),
      ];
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );

    await viewModel.load('item-1');

    expect(viewModel.children, hasLength(1));
    expect(viewModel.children.single.title, 'A sub-item');
  });

  test(
    'statusColorFor resolves a status by id, or null when unknown',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      expect(viewModel.statusColorFor('todo'), const Color(0xFF00FF00));
      expect(viewModel.statusColorFor('not-a-status'), isNull);
    },
  );

  test('saveDetails updates the item and returns true on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      typeId: 'type-task',
      priorityId: 'priority-high',
    );

    expect(ok, isTrue);
    expect(viewModel.item!.typeId, 'type-task');
    expect(viewModel.item!.priorityId, 'priority-high');
    expect(viewModel.saveError, isNull);
  });

  test('saveDetails returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('conflict'),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      typeId: null,
      priorityId: null,
    );

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test(
    'saveDetails sends the loaded version, then the version each save returns',
    () async {
      final repository = _FakeRepository()..item = _item(version: 5);
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveDetails(
        title: 'One',
        description: null,
        typeId: null,
        priorityId: null,
      );
      await viewModel.saveTags(['urgent']);

      expect(repository.expectedVersions, [5, 6]);
    },
  );

  test(
    'a save conflict after the dialog closed still refreshes the shared copy, '
    'which the board shows',
    () async {
      final repository = _FakeRepository(
        saveError: const ApiException('changed', statusCode: 409),
      )..assignGate = Completer<void>();
      final items = WorkItemStore();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        items,
      );
      await viewModel.load('item-1');
      repository.item = _item(title: "Someone else's title", version: 9);

      final save = viewModel.saveAssignee('user-1');
      viewModel.dispose();
      repository.assignGate!.complete();

      expect(await save, isFalse);
      expect(items['item-1']?.title, "Someone else's title");
    },
  );

  test('overlapping saves are sent one at a time, each with the version the '
      'previous one returned', () async {
    final gate = Completer<void>();
    final repository = _FakeRepository()
      ..item = _item(version: 3)
      ..assignGate = gate;
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final assign = viewModel.saveAssignee('user-1');
    final tags = viewModel.saveTags(['urgent']);
    await pumpEventQueue();
    expect(repository.updateTagsCalls, isEmpty, reason: 'waits its turn');

    gate.complete();

    expect(await assign, isTrue);
    expect(await tags, isTrue);
    expect(repository.expectedVersions, [3, 4]);
    expect(viewModel.item!.assignedToUserId, 'user-1');
    expect(viewModel.item!.tags, ['urgent']);
    expect(viewModel.saveError, isNull);
  });

  test('a delete issued while a save is in flight waits for it and sends the '
      'new version', () async {
    final gate = Completer<void>();
    final repository = _FakeRepository()..assignGate = gate;
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final save = viewModel.saveAssignee('user-1');
    final delete = viewModel.deleteItem();
    gate.complete();

    expect(await save, isTrue);
    expect(await delete, isTrue);
    expect(repository.expectedVersions, [1, 2]);
    expect(repository.deleted, isTrue);
  });

  test('a save puts the saved item in the shared store', () async {
    final items = WorkItemStore();
    final viewModel = WorkItemDetailViewModel(
      _FakeRepository(),
      _currentUser,
      items,
    );
    await viewModel.load('item-1');

    await viewModel.saveTags(['urgent']);

    expect(items['item-1']?.tags, ['urgent']);
    expect(items['item-1']?.version, 2);
  });

  test(
    'a save conflict reloads the latest item and bumps reloadGeneration',
    () async {
      final repository = _FakeRepository(
        saveError: const ApiException('changed', statusCode: 409),
      );
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      final generationBefore = viewModel.reloadGeneration;
      repository.item = _item(title: "Someone else's title", version: 9);

      final ok = await viewModel.saveDetails(
        title: 'My title',
        description: null,
        typeId: null,
        priorityId: null,
      );

      expect(ok, isFalse);
      expect(viewModel.item!.title, "Someone else's title");
      expect(viewModel.item!.version, 9);
      expect(viewModel.reloadGeneration, generationBefore + 1);
      expect(
        viewModel.saveError,
        contains('Someone else changed this work item'),
      );
      expect(viewModel.isSaving, isFalse);
    },
  );

  group('custom fields', () {
    _FakeRepository roomRepository({
      Map<String, Object?> fields = const {},
      Exception? saveError,
    }) => _FakeRepository(saveError: saveError)
      ..item = _item(typeId: 'type-room', fields: fields)
      ..fieldsByType = {
        'type-room': [_budget],
        'type-task': [_effort],
      };

    test("load offers the item's Type's fields", () async {
      final repository = roomRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );

      await viewModel.load('item-1');

      expect(viewModel.customFields, [_budget]);
    });

    test('an item without a Type has no fields to load', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );

      await viewModel.load('item-1');

      expect(viewModel.customFields, isEmpty);
      expect(repository.loadCustomFieldsCalls, isEmpty);
    });

    test(
      'saveCustomField sends only that field, with the item version',
      () async {
        final repository = roomRepository();
        final viewModel = WorkItemDetailViewModel(
          repository,
          _currentUser,
          WorkItemStore(),
        );
        await viewModel.load('item-1');

        final saved = await viewModel.saveCustomField('field-budget', 1200);

        expect(saved, isTrue);
        expect(repository.setCustomFieldsCalls, [
          {'field-budget': 1200},
        ]);
        expect(repository.expectedVersions, [1]);
        expect(viewModel.item!.fields, {'field-budget': 1200});
      },
    );

    test(
      'saveCustomField shows the server reason for a rejected value',
      () async {
        final repository = roomRepository(
          saveError: const ApiException(
            'Budget takes a number.',
            statusCode: 400,
          ),
        );
        final viewModel = WorkItemDetailViewModel(
          repository,
          _currentUser,
          WorkItemStore(),
        );
        await viewModel.load('item-1');

        final saved = await viewModel.saveCustomField('field-budget', 'lots');

        expect(saved, isFalse);
        expect(viewModel.saveError, 'Budget takes a number.');
      },
    );

    test("changing the Type loads the new Type's fields", () async {
      final repository = roomRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveDetails(
        title: 'A task',
        description: null,
        typeId: 'type-task',
        priorityId: null,
      );

      expect(viewModel.customFields, [_effort]);
    });

    test(
      "a conflict that brings in another Type loads that Type's fields",
      () async {
        final repository = roomRepository();
        final viewModel = WorkItemDetailViewModel(
          repository,
          _currentUser,
          WorkItemStore(),
        );
        await viewModel.load('item-1');
        // Someone else changed the Type since this dialog loaded.
        repository.item = _item(typeId: 'type-task', version: 2);

        final saved = await viewModel.saveCustomField('field-budget', 1);

        expect(saved, isFalse);
        expect(viewModel.item!.typeId, 'type-task');
        expect(viewModel.customFields, [_effort]);
      },
    );

    test(
      'typeChangeClearsFields says when a Type change would lose values',
      () async {
        final viewModel = WorkItemDetailViewModel(
          roomRepository(fields: {'field-budget': 1200}),
          _currentUser,
          WorkItemStore(),
        );
        await viewModel.load('item-1');

        expect(viewModel.typeChangeClearsFields('type-task'), isTrue);
        expect(viewModel.typeChangeClearsFields('type-room'), isFalse);
      },
    );
  });

  group('toggleDescriptionTask', () {
    test('saves the description with only that task toggled', () async {
      final repository = _FakeRepository()
        ..item = _item(description: '- [ ] milk\n- [ ] eggs', version: 4);
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.toggleDescriptionTask(1);

      expect(ok, isTrue);
      expect(repository.updateDescriptionCalls, ['- [ ] milk\n- [x] eggs']);
      expect(repository.expectedVersions, [4]);
      expect(viewModel.item!.description, '- [ ] milk\n- [x] eggs');
    });

    test('on a conflict, reloads the item and says so', () async {
      final repository = _FakeRepository(
        saveError: const ApiException('changed', statusCode: 409),
      )..item = _item(description: '- [ ] milk');
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      final generationBefore = viewModel.reloadGeneration;
      repository.item = _item(description: '- [x] milk', version: 9);

      final ok = await viewModel.toggleDescriptionTask(0);

      expect(ok, isFalse);
      expect(viewModel.item!.description, '- [x] milk');
      expect(viewModel.reloadGeneration, generationBefore + 1);
      expect(
        viewModel.saveError,
        contains('Someone else changed this work item'),
      );
    });
  });

  test('a non-conflict ApiException does not reload the item', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('bad tag', statusCode: 400),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    final generationBefore = viewModel.reloadGeneration;

    final ok = await viewModel.saveTags(['  ']);

    expect(ok, isFalse);
    expect(viewModel.reloadGeneration, generationBefore);
    // A 400 shows the server's validation message.
    expect(viewModel.saveError, 'bad tag');
  });

  test('saveAssignee updates the assignee on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.saveAssignee('user-1');

    expect(ok, isTrue);
    expect(viewModel.item!.assignedToUserId, 'user-1');
    expect(repository.assignCalls, ['user-1']);
  });

  test('saveAssignee and deleteItem send the loaded version', () async {
    final repository = _FakeRepository()..item = _item(version: 5);
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    await viewModel.saveAssignee('user-1');
    repository.item = _item(version: 9);
    await viewModel.load('item-1');
    await viewModel.deleteItem();

    expect(repository.expectedVersions, [5, 9]);
  });

  test('a delete conflict reloads the latest item', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('changed', statusCode: 409),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    repository.item = _item(title: "Someone else's title", version: 9);

    final ok = await viewModel.deleteItem();

    expect(ok, isFalse);
    expect(viewModel.item!.version, 9);
    expect(
      viewModel.saveError,
      contains('Someone else changed this work item'),
    );
  });

  test('saveTags updates the tag list on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.saveTags(['urgent', 'needs review']);

    expect(ok, isTrue);
    expect(viewModel.item!.tags, ['urgent', 'needs review']);
    expect(repository.updateTagsCalls, [
      ['urgent', 'needs review'],
    ]);
  });

  test('saveTags returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('invalid tag'),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.saveTags(['urgent']);

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test(
    'saveSchedule delegates to the repository and returns success',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveSchedule(DateTime(2026, 2, 1), null);

      expect(ok, isTrue);
      expect(repository.rescheduleCalls, ['item-1']);
    },
  );

  group('saveWhen', () {
    test('saves the trimmed phrase with the loaded version', () async {
      final repository = _FakeRepository()..item = _item(version: 4);
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveWhen('  after drywall ');

      expect(ok, isTrue);
      expect(repository.updateWhenCalls, ['after drywall']);
      expect(repository.expectedVersions, [4]);
      expect(viewModel.item!.when, 'after drywall');
    });

    test('a blank phrase clears the When', () async {
      final repository = _FakeRepository()..item = _item(when: 'next spring');
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveWhen('   ');

      expect(repository.updateWhenCalls, [null]);
    });

    test('an unchanged phrase saves nothing', () async {
      final repository = _FakeRepository()..item = _item(when: 'next spring');
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveWhen('next spring ');

      expect(ok, isTrue);
      expect(repository.updateWhenCalls, isEmpty);
      expect(viewModel.hasChanges, isFalse);
    });
  });

  group('applyQuickPick', () {
    test('sets the dates the server resolved the pick to', () async {
      final repository = _FakeRepository()
        ..quickPicks = [
          QuickPickDates(
            pick: QuickPick.thisWeekend,
            startDate: DateTime(2026, 11, 7),
            endDate: DateTime(2026, 11, 8),
          ),
          QuickPickDates(
            pick: QuickPick.nextWeek,
            startDate: DateTime(2026, 11, 9),
            endDate: DateTime(2026, 11, 15),
          ),
        ];
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.applyQuickPick(QuickPick.nextWeek);

      expect(ok, isTrue);
      expect(repository.rescheduledDates, [
        (DateTime(2026, 11, 9), DateTime(2026, 11, 15)),
      ]);
      expect(repository.updateWhenCalls, isEmpty);
    });

    test('reports a failure when the dates fail to save', () async {
      final repository = _FakeRepository(saveError: const ApiException('boom'))
        ..quickPicks = [
          QuickPickDates(
            pick: QuickPick.nextMonth,
            startDate: DateTime(2026, 12, 1),
            endDate: DateTime(2026, 12, 31),
          ),
        ];
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.applyQuickPick(QuickPick.nextMonth);

      expect(ok, isFalse);
      expect(viewModel.saveError, isNotNull);
    });
  });

  test('addComment appends the new comment to the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.addComment('Looks good');

    expect(ok, isTrue);
    expect(viewModel.comments, hasLength(1));
    expect(viewModel.comments.single.body, 'Looks good');
  });

  test('addComment returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: const ApiException('down'));
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.addComment('Looks good');

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test('updateComment changes the body of the existing comment', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    await viewModel.addComment('Original');
    final commentId = viewModel.comments.single.id;

    final ok = await viewModel.updateComment(commentId, 'Edited');

    expect(ok, isTrue);
    expect(viewModel.comments.single.body, 'Edited');
    expect(viewModel.comments.single.updatedAt, isNotNull);
  });

  test('deleteComment removes the comment from the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    await viewModel.addComment('Original');
    final commentId = viewModel.comments.single.id;

    final ok = await viewModel.deleteComment(commentId);

    expect(ok, isTrue);
    expect(viewModel.comments, isEmpty);
  });

  test('deleteItem calls the repository and returns true on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.deleteItem();

    expect(ok, isTrue);
    expect(repository.deleted, isTrue);
  });

  test('deleteItem returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('has children'),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.deleteItem();

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  group('links', () {
    WorkItemDetailViewModel loadedViewModel(_FakeRepository repository) =>
        WorkItemDetailViewModel(repository, _currentUser, WorkItemStore());

    test('load lists the work-item links and the URL links', () async {
      final repository = _FakeRepository()
        ..linksList.add(
          const WorkItemLink(
            id: 'link-1',
            type: WorkItemLinkRelation.blockedBy,
            linkedWorkItemId: 'item-2',
            linkedWorkItemNumber: 2,
            linkedWorkItemTitle: 'Drywall',
            linkedWorkItemSpaceId: 'home',
          ),
        )
        ..urlLinksList.add(
          const WorkItemUrlLink(id: 'url-1', url: 'https://example.com'),
        );
      final viewModel = loadedViewModel(repository);

      await viewModel.load('item-1');

      expect(viewModel.links.single.type, WorkItemLinkRelation.blockedBy);
      expect(viewModel.urlLinks.single.url, 'https://example.com');
    });

    test('addLink links the picked item as the chosen type', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.addLink(
        _item(id: 'item-2', number: 2),
        WorkItemLinkRelation.blocks,
      );

      expect(ok, isTrue);
      expect(repository.addLinkCalls, [
        ('item-2', WorkItemLinkRelation.blocks),
      ]);
      expect(viewModel.links.single.linkedWorkItemId, 'item-2');
      expect(viewModel.links.single.type, WorkItemLinkRelation.blocks);
    });

    test(
      "addLink shows the server's reason when it refuses the link",
      () async {
        final repository = _FakeRepository(
          saveError: const ApiException(
            'These items already block each other (409).',
            statusCode: 409,
          ),
        );
        final viewModel = loadedViewModel(repository);
        await viewModel.load('item-1');

        final ok = await viewModel.addLink(
          _item(id: 'item-2', number: 2),
          WorkItemLinkRelation.blocks,
        );

        expect(ok, isFalse);
        expect(
          viewModel.saveError,
          'These items already block each other (409).',
        );
        expect(viewModel.links, isEmpty);
      },
    );

    test(
      "addLink shows the server's message when it refuses a self link",
      () async {
        final repository = _FakeRepository(
          saveError: const ApiException(
            'A work item cannot link to itself.',
            statusCode: 400,
          ),
        );
        final viewModel = loadedViewModel(repository);
        await viewModel.load('item-1');

        final ok = await viewModel.addLink(
          _item(),
          WorkItemLinkRelation.related,
        );

        expect(ok, isFalse);
        expect(viewModel.saveError, 'A work item cannot link to itself.');
      },
    );

    test('adding a blocked-by link reloads the item, whose When the server '
        'now counts as resolved', () async {
      final repository = _FakeRepository()..item = _item(when: 'after drywall');
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');
      expect(viewModel.item!.whenResolved, isFalse);
      repository.item = _item(when: 'after drywall', whenResolved: true);

      final ok = await viewModel.addLink(
        _item(id: 'item-2', number: 2),
        WorkItemLinkRelation.blockedBy,
      );

      expect(ok, isTrue);
      expect(viewModel.item!.whenResolved, isTrue);
    });

    test('removing a link reloads the item, since its When may no longer '
        'be resolved', () async {
      final repository = _FakeRepository()..item = _item(when: 'after drywall');
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');
      repository.item = _item(when: 'after drywall', whenResolved: true);
      await viewModel.addLink(
        _item(id: 'item-2', number: 2),
        WorkItemLinkRelation.blockedBy,
      );
      repository.item = _item(when: 'after drywall');

      final ok = await viewModel.deleteLink(viewModel.links.single.id);

      expect(ok, isTrue);
      expect(viewModel.item!.whenResolved, isFalse);
    });

    test(
      'a link still counts as added when reloading the item fails',
      () async {
        final repository = _FakeRepository();
        final viewModel = loadedViewModel(repository);
        await viewModel.load('item-1');
        repository.getItemError = const ApiException('network down');

        final ok = await viewModel.addLink(
          _item(id: 'item-2', number: 2),
          WorkItemLinkRelation.blockedBy,
        );

        expect(ok, isTrue);
        expect(viewModel.saveError, isNull);
        expect(viewModel.links, hasLength(1));
        expect(viewModel.item, isNotNull);
      },
    );

    test('addLink reports other failures without the server detail', () async {
      final repository = _FakeRepository(
        saveError: const ApiException('Failed to add link (500).'),
      );
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.addLink(
        _item(id: 'item-2', number: 2),
        WorkItemLinkRelation.related,
      );

      expect(ok, isFalse);
      expect(viewModel.saveError, 'Could not add this link. Try again.');
    });

    test(
      'searchLinkTargets searches every Space, leaving out the item',
      () async {
        final repository = _FakeRepository()
          ..searchResults = [_item(), _item(id: 'other-space', number: 7)];
        final viewModel = loadedViewModel(repository);
        await viewModel.load('item-1');

        final found = await viewModel.searchLinkTargets('#7');

        expect(repository.allSpacesSearchCalls, ['#7']);
        expect(repository.searchCalls, isEmpty);
        expect(found.map((i) => i.id), ['other-space']);
      },
    );

    test('deleteLink removes the link from the list', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');
      await viewModel.addLink(
        _item(id: 'item-2'),
        WorkItemLinkRelation.related,
      );

      final ok = await viewModel.deleteLink(viewModel.links.single.id);

      expect(ok, isTrue);
      expect(viewModel.links, isEmpty);
    });

    test('addUrlLink adds the trimmed URL and label', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.addUrlLink(
        ' https://example.com/pr/7 ',
        label: ' PR 7 ',
      );

      expect(ok, isTrue);
      expect(repository.addUrlLinkCalls, [
        ('https://example.com/pr/7', 'PR 7'),
      ]);
      expect(viewModel.urlLinks.single.displayName, 'PR 7');
    });

    test('addUrlLink sends no label when it is blank', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      await viewModel.addUrlLink('https://example.com', label: '  ');

      expect(repository.addUrlLinkCalls, [('https://example.com', null)]);
    });

    test('addUrlLink refuses a URL that is not absolute http(s) without '
        'sending it', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.addUrlLink('example.com/pr/7');

      expect(ok, isFalse);
      expect(viewModel.saveError, isNotNull);
      expect(repository.addUrlLinkCalls, isEmpty);
    });

    test("addUrlLink shows the server's validation message", () async {
      final repository = _FakeRepository(
        saveError: const ApiException(
          'The URL must be an absolute http or https URL. (400).',
          statusCode: 400,
        ),
      );
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.addUrlLink('https://example.com');

      expect(ok, isFalse);
      expect(
        viewModel.saveError,
        'The URL must be an absolute http or https URL. (400).',
      );
    });

    test('deleteUrlLink removes the URL link from the list', () async {
      final repository = _FakeRepository();
      final viewModel = loadedViewModel(repository);
      await viewModel.load('item-1');
      await viewModel.addUrlLink('https://example.com');

      final ok = await viewModel.deleteUrlLink(viewModel.urlLinks.single.id);

      expect(ok, isTrue);
      expect(viewModel.urlLinks, isEmpty);
    });
  });

  group('hasChanges', () {
    test('is false after just loading', () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(),
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      expect(viewModel.hasChanges, isFalse);
    });

    test('stays false after a save, which the store carries to other '
        'views', () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(),
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveAssignee('user-1');

      expect(viewModel.hasChanges, isFalse);
    });

    test('stays false after a failed save', () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(saveError: const ApiException('boom')),
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveTags(['urgent']);

      expect(viewModel.hasChanges, isFalse);
    });

    test(
      'stays false after deleting the item, which leaves the shared store',
      () async {
        final items = WorkItemStore();
        final viewModel = WorkItemDetailViewModel(
          _FakeRepository(),
          _currentUser,
          items,
        );
        await viewModel.load('item-1');

        await viewModel.deleteItem();

        expect(viewModel.hasChanges, isFalse);
        expect(items['item-1'], isNull);
        expect(viewModel.item, isNull);
      },
    );

    test('a nested dialog that changed something reloads the Sub-Items list '
        'and marks this dialog changed', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      repository.childrenList = [
        const WorkItemChildSummary(
          id: 'child-2',
          number: 7,
          title: 'Remaining',
          statusId: 'todo',
        ),
      ];

      await viewModel.onNestedDialogClosed(changed: true);

      expect(viewModel.children.single.id, 'child-2');
      expect(viewModel.hasChanges, isTrue);
    });

    test('a nested dialog that only edited its item still reloads the '
        'Sub-Items list, without marking this dialog changed', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      repository.childrenList = [
        const WorkItemChildSummary(
          id: 'child-1',
          number: 7,
          title: 'Renamed in its own dialog',
          statusId: 'todo',
        ),
      ];

      await viewModel.onNestedDialogClosed(changed: false);

      expect(viewModel.children.single.title, 'Renamed in its own dialog');
      expect(viewModel.hasChanges, isFalse);
    });
  });

  test(
    'isSaving stays true until every overlapping save has finished',
    () async {
      final gate = Completer<void>();
      final repository = _FakeRepository()..assignGate = gate;
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final slowSave = viewModel.saveAssignee('user-1');
      final queuedSave = viewModel.saveTags(['urgent']);
      await pumpEventQueue();
      expect(viewModel.isSaving, isTrue);

      gate.complete();
      await slowSave;
      await queuedSave;
      expect(viewModel.isSaving, isFalse);
    },
  );

  test(
    'comment mutations apply the server response instead of reloading',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      final loadsAfterOpen = repository.loadCommentsCalls;

      await viewModel.addComment('First');
      final id = viewModel.comments.single.id;
      await viewModel.updateComment(id, 'Edited');
      expect(viewModel.comments.single.body, 'Edited');
      await viewModel.deleteComment(id);

      expect(viewModel.comments, isEmpty);
      expect(repository.loadCommentsCalls, loadsAfterOpen);
    },
  );

  test(
    'a programming error during load is rethrown, not shown as a load failure',
    () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(getItemError: StateError('bug')),
        _currentUser,
        WorkItemStore(),
      );

      await expectLater(viewModel.load('item-1'), throwsStateError);
      expect(viewModel.loadError, isNull);
    },
  );

  test("isOwnComment is true only for the signed-in user's comments", () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    await viewModel.addComment('Mine');
    final mine = viewModel.comments.single;
    final theirs = WorkItemComment(
      id: 'other',
      workItemId: 'item-1',
      authorUserId: 'user-2',
      authorUsername: 'bob',
      body: 'Theirs',
      createdAt: DateTime(2026, 9, 26),
      updatedAt: null,
    );

    expect(viewModel.isOwnComment(mine), isTrue);
    expect(viewModel.isOwnComment(theirs), isFalse);
  });

  group('validation before saving', () {
    test('a blank title is rejected without calling the server', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveDetails(
        title: '   ',
        description: null,
        typeId: null,
        priorityId: null,
      );

      expect(ok, isFalse);
      expect(viewModel.saveError, 'Title is required.');
      expect(repository.updateDetailsCalls, isEmpty);
    });

    test('titleError flags blank and overlong titles only', () {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(),
        _currentUser,
        WorkItemStore(),
      );

      expect(viewModel.titleError('A task'), isNull);
      expect(viewModel.titleError(''), isNotNull);
      expect(
        viewModel.titleError(
          'x' * (WorkItemDetailViewModel.titleMaxLength + 1),
        ),
        isNotNull,
      );
    });

    test(
      'an inverted schedule is rejected without calling the server',
      () async {
        final repository = _FakeRepository();
        final viewModel = WorkItemDetailViewModel(
          repository,
          _currentUser,
          WorkItemStore(),
        );
        await viewModel.load('item-1');

        final ok = await viewModel.saveSchedule(
          DateTime(2026, 9, 30),
          DateTime(2026, 9, 1),
        );

        expect(ok, isFalse);
        expect(viewModel.saveError, contains('start date'));
        expect(repository.rescheduleCalls, isEmpty);
      },
    );
  });

  group('saveStatus', () {
    test('moves the item to the status with the loaded version', () async {
      final repository = _FakeRepository()..item = _item(version: 4);
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveStatus('done');

      expect(ok, isTrue);
      expect(repository.changeStatusCalls, ['done']);
      expect(repository.expectedVersions, [4]);
      expect(viewModel.item!.statusId, 'done');
    });

    test('marks the dialog changed, since the board must re-rank the card '
        'in its new column', () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(),
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveStatus('done');

      expect(viewModel.hasChanges, isTrue);
    });

    test('the current status saves nothing', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      await viewModel.saveStatus('todo');

      expect(repository.changeStatusCalls, isEmpty);
      expect(viewModel.hasChanges, isFalse);
    });
  });

  group('parent', () {
    final kitchen = _item(id: 'kitchen', number: 5, title: 'Kitchen');
    final garden = _item(id: 'garden', number: 9, title: 'Garden');

    test('load fetches the parent, to show its number and title', () async {
      final repository = _FakeRepository()
        ..item = _item(parentId: 'kitchen')
        ..others['kitchen'] = kitchen;
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );

      await viewModel.load('item-1');

      expect(viewModel.parent?.number, 5);
      expect(viewModel.parent?.title, 'Kitchen');
    });

    test('a top-level item has no parent to fetch', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );

      await viewModel.load('item-1');

      expect(viewModel.parent, isNull);
      expect(repository.getItemCalls, 1);
    });

    test('saveParent moves the item under the picked item with the loaded '
        'version, and marks the dialog changed', () async {
      final repository = _FakeRepository()..item = _item(version: 3);
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveParent(garden);

      expect(ok, isTrue);
      expect(repository.reparentCalls, ['garden']);
      expect(repository.expectedVersions, [3]);
      expect(viewModel.item!.parentId, 'garden');
      expect(viewModel.parent?.title, 'Garden');
      expect(viewModel.hasChanges, isTrue);
    });

    test("onMovedToSpace reloads the new Space's lookups, re-seeds the "
        'form and marks the dialog changed', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');
      final generation = viewModel.reloadGeneration;
      repository.item = workItem('item-1', spaceId: 'workshop', version: 2);

      await viewModel.onMovedToSpace();

      expect(repository.lookupSpaceIds.skip(3), [
        'workshop',
        'workshop',
        'workshop',
      ]);
      expect(viewModel.item!.spaceId, 'workshop');
      expect(viewModel.reloadGeneration, greaterThan(generation));
      expect(viewModel.hasChanges, isTrue);
    });

    test('saveParent with no item makes it top-level', () async {
      final repository = _FakeRepository()
        ..item = _item(parentId: 'kitchen')
        ..others['kitchen'] = kitchen;
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveParent(null);

      expect(ok, isTrue);
      expect(repository.reparentCalls, [null]);
      expect(viewModel.item!.parentId, isNull);
      expect(viewModel.parent, isNull);
    });

    test('a parent inside the item is refused with a message naming it, '
        'and nothing else changes', () async {
      final repository = _FakeRepository()
        ..item = _item(parentId: 'kitchen')
        ..others['kitchen'] = kitchen
        ..descendantIds = {'garden'};
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.saveParent(garden);

      expect(ok, isFalse);
      expect(
        viewModel.saveError,
        "#9 Garden is inside this work item, so it can't be its parent.",
      );
      expect(viewModel.item!.parentId, 'kitchen');
      expect(viewModel.parent?.title, 'Kitchen');
      expect(viewModel.reloadGeneration, 0);
      expect(viewModel.hasChanges, isFalse);
    });

    test("searchParents searches the item's Space, leaving out the item and "
        'its sub-items', () async {
      final repository = _FakeRepository()
        ..childrenList = [
          const WorkItemChildSummary(
            id: 'child-1',
            number: 2,
            title: 'Child',
            statusId: 'todo',
          ),
        ]
        ..searchResults = [
          _item(),
          kitchen,
          _item(id: 'child-1', number: 2),
          garden,
        ];
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final found = await viewModel.searchParents('ki');

      expect(repository.searchCalls, [('home', 'ki')]);
      expect(found.map((i) => i.id), ['kitchen', 'garden']);
    });
  });

  group('addSubItem', () {
    test("creates a child in the Space's first status and lists it", () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.addSubItem('  Paint the walls ');

      expect(ok, isTrue);
      expect(repository.createSubItemCalls, [
        ('item-1', 'Paint the walls', 'todo'),
      ]);
      expect(viewModel.children.single.title, 'Paint the walls');
      expect(viewModel.hasChanges, isTrue);
    });

    test('a blank title is rejected without calling the server', () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.addSubItem('   ');

      expect(ok, isFalse);
      expect(repository.createSubItemCalls, isEmpty);
      expect(viewModel.saveError, 'Title is required.');
    });

    test('a failure says so and lists nothing new', () async {
      final repository = _FakeRepository(
        saveError: const ApiException('boom', statusCode: 500),
      );
      final viewModel = WorkItemDetailViewModel(
        repository,
        _currentUser,
        WorkItemStore(),
      );
      await viewModel.load('item-1');

      final ok = await viewModel.addSubItem('Paint');

      expect(ok, isFalse);
      expect(viewModel.saveError, 'Could not add the sub-item. Try again.');
      expect(viewModel.children, isEmpty);
      expect(viewModel.hasChanges, isFalse);
    });
  });
}
