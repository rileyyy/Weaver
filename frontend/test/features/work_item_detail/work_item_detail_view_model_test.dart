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
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
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
  String statusId = 'todo',
  String? typeId,
  String? priorityId,
  String? assignedToUserId,
  List<String> tags = const [],
  String title = 'A task',
  String? description,
  String? when,
  Map<String, Object?> fields = const {},
  int version = 1,
}) => WorkItem(
  number: 1,
  id: id,
  spaceId: 'home',
  parentId: null,
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
);

class _FakeCurrentUser implements CurrentUser {
  const _FakeCurrentUser(this.currentUserId);

  @override
  final String? currentUserId;
}

const _currentUser = _FakeCurrentUser('user-1');

class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.getItemError, this.saveError});

  final Object? getItemError;
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
  List<WorkItemChildSummary> childrenList = [];
  int loadCommentsCalls = 0;
  int getItemCalls = 0;
  int loadLinksCalls = 0;
  final List<String> lookupSpaceIds = [];
  Map<String, List<CustomField>> fieldsByType = {};
  final List<String> loadCustomFieldsCalls = [];
  final List<Map<String, Object?>> setCustomFieldsCalls = [];

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
    return item;
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
  ) async {
    final error = saveError;
    if (error != null) throw error;
    final link = WorkItemLink(
      id: 'link-${linksList.length + 1}',
      linkedWorkItemId: targetWorkItemId,
      linkedWorkItemTitle: 'Target $targetWorkItemId',
    );
    linksList.add(link);
    return link;
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

  test('statusName resolves the current status from the loaded list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    expect(viewModel.statusName, 'To Do');
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
      'clearsCustomFields says when a Type change would lose values',
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

  test('addLink appends the new link to the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.addLink('item-2');

    expect(ok, isTrue);
    expect(viewModel.links, hasLength(1));
    expect(viewModel.links.single.linkedWorkItemId, 'item-2');
  });

  test('addLink returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('duplicate'),
    );
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');

    final ok = await viewModel.addLink('item-2');

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
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

  test('deleteLink removes the link from the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(
      repository,
      _currentUser,
      WorkItemStore(),
    );
    await viewModel.load('item-1');
    await viewModel.addLink('item-2');
    final linkId = viewModel.links.single.id;

    final ok = await viewModel.deleteLink(linkId);

    expect(ok, isTrue);
    expect(viewModel.links, isEmpty);
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
}
