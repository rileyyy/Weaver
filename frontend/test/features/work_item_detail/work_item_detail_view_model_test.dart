import 'dart:async';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

WorkItemDetail _item({
  String id = 'item-1',
  String statusId = 'todo',
  String? layerId,
  WorkItemPriority priority = WorkItemPriority.medium,
  String? assignedToUserId,
  List<String> tags = const [],
  String title = 'A task',
  int version = 1,
}) => WorkItemDetail(
  id: id,
  parentId: null,
  title: title,
  description: null,
  statusId: statusId,
  layerId: layerId,
  priority: priority,
  assignedToUserId: assignedToUserId,
  startDate: null,
  endDate: null,
  createdAt: DateTime.utc(2026, 1, 1),
  updatedAt: DateTime.utc(2026, 1, 1),
  version: version,
  tags: tags,
);

class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.getItemError, this.saveError});

  final Exception? getItemError;
  final Exception? saveError;
  WorkItemDetail item = _item();
  bool deleted = false;
  final List<String> updateDetailsCalls = [];
  final List<String?> assignCalls = [];
  final List<String> rescheduleCalls = [];
  final List<List<String>> updateTagsCalls = [];
  final List<int> expectedVersions = [];
  final List<WorkItemComment> commentsList = [];
  final List<WorkItemLink> linksList = [];
  List<WorkItemChildSummary> childrenList = [];
  int loadCommentsCalls = 0;
  int loadLinksCalls = 0;

  /// When set, [assign] waits on it, so a test can overlap two saves.
  Completer<void>? assignGate;

  @override
  Future<WorkItemDetail> getItem(String id) async {
    final error = getItemError;
    if (error != null) throw error;
    return item;
  }

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async =>
      List.of(childrenList);

  @override
  Future<void> deleteItem(String id, {bool cascade = false}) async {
    final error = saveError;
    if (error != null) throw error;
    deleted = true;
  }

  @override
  Future<List<BoardStatus>> loadStatuses() async => const [
    BoardStatus(id: 'todo', name: 'To Do', order: 0, color: Color(0xFF00FF00)),
    BoardStatus(id: 'done', name: 'Done', order: 1),
  ];

  @override
  Future<List<WorkItemLayer>> loadLayers() async => const [
    WorkItemLayer(id: 'layer-task', name: 'Task', order: 0),
  ];

  @override
  Future<List<AuthUser>> loadUsers() async => const [
    AuthUser(id: 'user-1', username: 'alice', kind: UserKind.human),
  ];

  @override
  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
    required int expectedVersion,
  }) async {
    updateDetailsCalls.add(id);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    return item = _item(
      id: id,
      statusId: item.statusId,
      layerId: layerId,
      priority: priority,
      version: item.version + 1,
    );
  }

  @override
  Future<WorkItemDetail> assign(String id, String? userId) async {
    assignCalls.add(userId);
    await assignGate?.future;
    final error = saveError;
    if (error != null) throw error;
    return item = _item(
      id: id,
      statusId: item.statusId,
      assignedToUserId: userId,
    );
  }

  @override
  Future<WorkItemDetail> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) async {
    rescheduleCalls.add(id);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    return item;
  }

  @override
  Future<WorkItemDetail> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  }) async {
    updateTagsCalls.add(tags);
    expectedVersions.add(expectedVersion);
    final error = saveError;
    if (error != null) throw error;
    return item = _item(id: id, statusId: item.statusId, tags: tags);
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
}

void main() {
  test('load populates item, statuses, layers, and users', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
    expect(viewModel.item!.id, 'item-1');
    expect(viewModel.statuses, hasLength(2));
    expect(viewModel.layers, hasLength(1));
    expect(viewModel.users, hasLength(1));
  });

  test('load sets loadError when the repository throws', () async {
    final repository = _FakeRepository(getItemError: Exception('network down'));
    final viewModel = WorkItemDetailViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.isLoading, isFalse);
  });

  test('statusName resolves the current status from the loaded list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
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
    final viewModel = WorkItemDetailViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.children, hasLength(1));
    expect(viewModel.children.single.title, 'A sub-item');
  });

  test(
    'statusColorFor resolves a status by id, or null when unknown',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(repository);
      await viewModel.load('item-1');

      expect(viewModel.statusColorFor('todo'), const Color(0xFF00FF00));
      expect(viewModel.statusColorFor('not-a-status'), isNull);
    },
  );

  test('saveDetails updates the item and returns true on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      layerId: 'layer-task',
      priority: WorkItemPriority.high,
    );

    expect(ok, isTrue);
    expect(viewModel.item!.layerId, 'layer-task');
    expect(viewModel.item!.priority, WorkItemPriority.high);
    expect(viewModel.saveError, isNull);
  });

  test('saveDetails returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('conflict'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      layerId: null,
      priority: WorkItemPriority.medium,
    );

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test(
    'saveDetails sends the loaded version, then the version each save returns',
    () async {
      final repository = _FakeRepository()..item = _item(version: 5);
      final viewModel = WorkItemDetailViewModel(repository);
      await viewModel.load('item-1');

      await viewModel.saveDetails(
        title: 'One',
        description: null,
        layerId: null,
        priority: WorkItemPriority.low,
      );
      await viewModel.saveTags(['urgent']);

      expect(repository.expectedVersions, [5, 6]);
    },
  );

  test(
    'a save conflict reloads the latest item and bumps reloadGeneration',
    () async {
      final repository = _FakeRepository(
        saveError: const ApiException('changed', statusCode: 409),
      );
      final viewModel = WorkItemDetailViewModel(repository);
      await viewModel.load('item-1');
      final generationBefore = viewModel.reloadGeneration;
      repository.item = _item(title: "Someone else's title", version: 9);

      final ok = await viewModel.saveDetails(
        title: 'My title',
        description: null,
        layerId: null,
        priority: WorkItemPriority.medium,
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

  test('a non-conflict ApiException does not reload the item', () async {
    final repository = _FakeRepository(
      saveError: const ApiException('bad tag', statusCode: 400),
    );
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');
    final generationBefore = viewModel.reloadGeneration;

    final ok = await viewModel.saveTags(['  ']);

    expect(ok, isFalse);
    expect(viewModel.reloadGeneration, generationBefore);
    expect(viewModel.saveError, 'Could not save your change. Try again.');
  });

  test('saveAssignee updates the assignee on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveAssignee('user-1');

    expect(ok, isTrue);
    expect(viewModel.item!.assignedToUserId, 'user-1');
    expect(repository.assignCalls, ['user-1']);
  });

  test('saveTags updates the tag list on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveTags(['urgent', 'needs review']);

    expect(ok, isTrue);
    expect(viewModel.item!.tags, ['urgent', 'needs review']);
    expect(repository.updateTagsCalls, [
      ['urgent', 'needs review'],
    ]);
  });

  test('saveTags returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('invalid tag'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveTags(['urgent']);

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test(
    'saveSchedule delegates to the repository and returns success',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(repository);
      await viewModel.load('item-1');

      final ok = await viewModel.saveSchedule(DateTime(2026, 2, 1), null);

      expect(ok, isTrue);
      expect(repository.rescheduleCalls, ['item-1']);
    },
  );

  test('addComment appends the new comment to the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.addComment('Looks good');

    expect(ok, isTrue);
    expect(viewModel.comments, hasLength(1));
    expect(viewModel.comments.single.body, 'Looks good');
  });

  test('addComment returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('down'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.addComment('Looks good');

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test('updateComment changes the body of the existing comment', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
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
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');
    await viewModel.addComment('Original');
    final commentId = viewModel.comments.single.id;

    final ok = await viewModel.deleteComment(commentId);

    expect(ok, isTrue);
    expect(viewModel.comments, isEmpty);
  });

  test('addLink appends the new link to the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.addLink('item-2');

    expect(ok, isTrue);
    expect(viewModel.links, hasLength(1));
    expect(viewModel.links.single.linkedWorkItemId, 'item-2');
  });

  test('addLink returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('duplicate'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.addLink('item-2');

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test('deleteItem calls the repository and returns true on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.deleteItem();

    expect(ok, isTrue);
    expect(repository.deleted, isTrue);
  });

  test('deleteItem returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('has children'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.deleteItem();

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test('deleteLink removes the link from the list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');
    await viewModel.addLink('item-2');
    final linkId = viewModel.links.single.id;

    final ok = await viewModel.deleteLink(linkId);

    expect(ok, isTrue);
    expect(viewModel.links, isEmpty);
  });

  group('hasChanges', () {
    test('is false after just loading', () async {
      final viewModel = WorkItemDetailViewModel(_FakeRepository());
      await viewModel.load('item-1');

      expect(viewModel.hasChanges, isFalse);
    });

    test('becomes true after a successful save', () async {
      final viewModel = WorkItemDetailViewModel(_FakeRepository());
      await viewModel.load('item-1');

      await viewModel.saveAssignee('user-1');

      expect(viewModel.hasChanges, isTrue);
    });

    test('stays false after a failed save', () async {
      final viewModel = WorkItemDetailViewModel(
        _FakeRepository(saveError: Exception('boom')),
      );
      await viewModel.load('item-1');

      await viewModel.saveTags(['urgent']);

      expect(viewModel.hasChanges, isFalse);
    });

    test('becomes true after deleting the item', () async {
      final viewModel = WorkItemDetailViewModel(_FakeRepository());
      await viewModel.load('item-1');

      await viewModel.deleteItem();

      expect(viewModel.hasChanges, isTrue);
    });

    test(
      'onSubItemChanged reloads the Sub-Items list and marks the dialog changed',
      () async {
        final repository = _FakeRepository();
        final viewModel = WorkItemDetailViewModel(repository);
        await viewModel.load('item-1');
        repository.childrenList = [
          const WorkItemChildSummary(
            id: 'child-2',
            number: 7,
            title: 'Remaining',
            statusId: 'todo',
          ),
        ];

        await viewModel.onSubItemChanged();

        expect(viewModel.children.single.id, 'child-2');
        expect(viewModel.hasChanges, isTrue);
      },
    );
  });

  test(
    'isSaving stays true until every overlapping save has finished',
    () async {
      final gate = Completer<void>();
      final repository = _FakeRepository()..assignGate = gate;
      final viewModel = WorkItemDetailViewModel(repository);
      await viewModel.load('item-1');

      final slowSave = viewModel.saveAssignee('user-1');
      await viewModel.saveTags(['urgent']);
      expect(viewModel.isSaving, isTrue);

      gate.complete();
      await slowSave;
      expect(viewModel.isSaving, isFalse);
    },
  );

  test(
    'comment mutations apply the server response instead of reloading',
    () async {
      final repository = _FakeRepository();
      final viewModel = WorkItemDetailViewModel(repository);
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
}
