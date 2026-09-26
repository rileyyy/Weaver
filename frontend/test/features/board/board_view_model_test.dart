import 'dart:async';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

const _todoColor = Color(0xFF1E88E5);

const _rootBoard = BoardData(
  statuses: [
    BoardStatus(id: 'done', name: 'Done', order: 1),
    BoardStatus(id: 'todo', name: 'To Do', order: 0, color: _todoColor),
  ],
  swimlanes: [
    Swimlane(
      parentId: 'lane-a',
      title: 'Lane A',
      cards: [
        WorkItemCard(
          id: 'card-1',
          number: 1,
          title: 'Card 1',
          parentId: 'lane-a',
          statusId: 'todo',
        ),
      ],
    ),
    Swimlane(
      parentId: 'lane-b',
      title: 'Lane B',
      cards: [
        WorkItemCard(
          id: 'card-2',
          number: 2,
          title: 'Card 2',
          parentId: 'lane-b',
          statusId: 'todo',
        ),
      ],
    ),
  ],
);

const _card1Children = BoardData(
  statuses: [BoardStatus(id: 'todo', name: 'To Do', order: 0)],
  swimlanes: [
    Swimlane(
      parentId: 'card-1',
      title: 'Card 1',
      cards: [
        WorkItemCard(
          id: 'grandchild-1',
          number: 3,
          title: 'Grandchild',
          parentId: 'card-1',
          statusId: 'todo',
        ),
      ],
    ),
  ],
);

/// A [BoardRepository] test double whose `loadBoard` only knows about the
/// root scope (`rootScopeId`) and `'card-1'` — anything else (e.g. drilling
/// into `card-2`, which has no fixture) errors, to exercise the
/// no-children/failure path.
class _TestBoardRepository implements BoardRepository {
  _TestBoardRepository({
    this.changeStatusError,
    this.reparentError,
    this.rescheduleError,
    this.createError,
    this.assignError,
    this.tagsError,
    this.hierarchyItems = const [],
    this.hierarchyError,
    this.users = const [],
  });

  final Exception? changeStatusError;
  final Exception? reparentError;
  final Exception? rescheduleError;
  final Exception? createError;
  final Exception? assignError;
  final Exception? tagsError;
  final List<HierarchyItem> hierarchyItems;
  final Exception? hierarchyError;
  final List<AuthUser> users;
  final List<String> statusChanges = [];
  final List<String> reparents = [];
  final List<String> reschedules = [];
  final List<String?> requestedScopes = [];
  final List<String> creates = [];

  /// When set, [createWorkItem] waits on it, so a test can navigate while
  /// a create is in flight.
  Completer<void>? createGate;

  /// Scope loads for these scope ids wait on their completer, so a test can
  /// finish overlapping loads in any order.
  final Map<String?, Completer<void>> loadGates = {};

  /// Status changes for these card ids wait on their completer; completing
  /// it with an error makes that one change fail.
  final Map<String, Completer<void>> statusGates = {};

  /// When non-null, each [loadAllItems] call parks a completer here instead
  /// of returning immediately.
  List<Completer<List<HierarchyItem>>>? pendingHierarchyLoads;
  final List<String> assigns = [];
  final List<String> tagUpdates = [];
  int loadAllItemsCallCount = 0;

  @override
  Future<String?> loadRootScopeItemId() => Future.value();

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async {
    requestedScopes.add(scopeItemId);
    await loadGates[scopeItemId]?.future;
    if (scopeItemId == null) return _rootBoard;
    if (scopeItemId == 'card-1') return _card1Children;
    throw StateError('No fixture for scope $scopeItemId');
  }

  @override
  Future<List<HierarchyItem>> loadAllItems() async {
    loadAllItemsCallCount++;
    final pending = pendingHierarchyLoads;
    if (pending != null) {
      final load = Completer<List<HierarchyItem>>();
      pending.add(load);
      return load.future;
    }
    final error = hierarchyError;
    if (error != null) throw error;
    return hierarchyItems;
  }

  @override
  Future<List<AuthUser>> loadUsers() => Future.value(users);

  @override
  Future<void> changeStatus(String cardId, String newStatusId) async {
    statusChanges.add('$cardId->$newStatusId');
    await statusGates[cardId]?.future;
    final error = changeStatusError;
    if (error != null) throw error;
  }

  @override
  Future<void> reparentItem(String itemId, String newParentId) async {
    reparents.add('$itemId->$newParentId');
    final error = reparentError;
    if (error != null) throw error;
  }

  @override
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  ) async {
    reschedules.add('$itemId->$startDate..$endDate');
    final error = rescheduleError;
    if (error != null) throw error;
  }

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) async {
    creates.add('$title->$parentId/$statusId');
    await createGate?.future;
    final error = createError;
    if (error != null) throw error;
  }

  @override
  Future<void> assign(String workItemId, String? userId) async {
    assigns.add('$workItemId->$userId');
    final error = assignError;
    if (error != null) throw error;
  }

  @override
  Future<void> setTags(String workItemId, List<String> tags) async {
    tagUpdates.add('$workItemId->${tags.join(',')}');
    final error = tagsError;
    if (error != null) throw error;
  }
}

class _FailingLoadRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() =>
      Future.error(Exception('network down'));

  @override
  Future<BoardData> loadBoard(String? scopeItemId) =>
      Future.error(Exception('network down'));

  @override
  Future<List<HierarchyItem>> loadAllItems() =>
      Future.error(Exception('network down'));

  @override
  Future<List<AuthUser>> loadUsers() => Future.value(const []);

  @override
  Future<void> changeStatus(String cardId, String newStatusId) =>
      Future.value();

  @override
  Future<void> reparentItem(String itemId, String newParentId) =>
      Future.value();

  @override
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  ) => Future.value();

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) => Future.value();

  @override
  Future<void> assign(String workItemId, String? userId) => Future.value();

  @override
  Future<void> setTags(String workItemId, List<String> tags) => Future.value();
}

void main() {
  late _TestBoardRepository repository;
  late BoardViewModel viewModel;

  setUp(() async {
    repository = _TestBoardRepository();
    viewModel = BoardViewModel(repository);
    await viewModel.load();
  });

  test('load sorts statuses by order', () {
    expect(viewModel.statuses.map((s) => s.id), ['todo', 'done']);
  });

  test('load sets the root breadcrumb', () {
    expect(viewModel.breadcrumbs, hasLength(1));
    expect(viewModel.breadcrumbs.single.title, 'Board');
  });

  test('load sets loadError when the repository throws', () async {
    final failingViewModel = BoardViewModel(_FailingLoadRepository());
    await failingViewModel.load();

    expect(failingViewModel.loadError, isNotNull);
    expect(failingViewModel.isLoading, isFalse);
  });

  test('moveCard changes the status of only the moved card', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(card, 'done');

    expect(viewModel.swimlanes[0].cards.single.statusId, 'done');
  });

  test('moveCard never moves a card into a different swimlane', () async {
    final cardInLaneA = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(cardInLaneA, 'done');

    expect(viewModel.swimlanes[0].cards.single.parentId, 'lane-a');
    expect(viewModel.swimlanes[1].cards.single.parentId, 'lane-b');
    expect(viewModel.swimlanes[1].cards.single.statusId, 'todo');
  });

  test('moveCard persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(card, 'done');

    expect(repository.statusChanges, ['card-1->done']);
  });

  test(
    'moveCard rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        changeStatusError: Exception('conflict'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.moveCard(card, 'done');

      expect(failingViewModel.swimlanes[0].cards.single.statusId, 'todo');
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test("drillInto loads the card's own children as swimlanes", () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.drillInto(card);

    expect(viewModel.swimlanes.single.parentId, 'card-1');
    expect(viewModel.swimlanes.single.cards.single.title, 'Grandchild');
    expect(viewModel.breadcrumbs.map((b) => b.title), ['Board', 'Card 1']);
  });

  test(
    'drillInto sets loadError and leaves the breadcrumb unchanged on failure',
    () async {
      final cardWithNoFixture = viewModel.swimlanes[1].cards.single;

      await viewModel.drillInto(cardWithNoFixture);

      expect(viewModel.loadError, isNotNull);
      expect(viewModel.breadcrumbs, hasLength(1));
    },
  );

  test('navigateToBreadcrumb returns to a previous scope', () async {
    final card = viewModel.swimlanes[0].cards.single;
    await viewModel.drillInto(card);

    await viewModel.navigateToBreadcrumb(0);

    expect(viewModel.breadcrumbs, hasLength(1));
    expect(
      viewModel.swimlanes.any((lane) => lane.parentId == 'lane-a'),
      isTrue,
    );
  });

  test('navigateToBreadcrumb is a no-op for the current scope', () async {
    final requestCountBefore = repository.requestedScopes.length;

    await viewModel.navigateToBreadcrumb(0);

    expect(repository.requestedScopes, hasLength(requestCountBefore));
  });

  test('retry re-issues the request that failed', () async {
    final cardWithNoFixture = viewModel.swimlanes[1].cards.single;
    await viewModel.drillInto(cardWithNoFixture);
    final failedAttempts = repository.requestedScopes
        .where((scope) => scope == 'card-2')
        .length;

    await viewModel.retry();

    final attemptsAfterRetry = repository.requestedScopes
        .where((scope) => scope == 'card-2')
        .length;
    expect(attemptsAfterRetry, failedAttempts + 1);
    expect(viewModel.loadError, isNotNull);
  });

  test(
    'reparentCard moves the card to the new swimlane, keeping its status',
    () async {
      final card = viewModel.swimlanes[0].cards.single;

      await viewModel.reparentCard(card, 'lane-b');

      expect(viewModel.swimlanes[0].cards, isEmpty);
      final movedCard = viewModel.swimlanes[1].cards.firstWhere(
        (c) => c.id == 'card-1',
      );
      expect(movedCard.parentId, 'lane-b');
      expect(movedCard.statusId, 'todo');
      expect(viewModel.swimlanes[1].cards, hasLength(2));
    },
  );

  test('reparentCard persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.reparentCard(card, 'lane-b');

    expect(repository.reparents, ['card-1->lane-b']);
  });

  test(
    'reparentCard is a no-op when the target is the current parent',
    () async {
      final card = viewModel.swimlanes[0].cards.single;

      await viewModel.reparentCard(card, 'lane-a');

      expect(repository.reparents, isEmpty);
    },
  );

  test(
    'reparentCard rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        reparentError: Exception('cycle'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.reparentCard(card, 'lane-b');

      expect(failingViewModel.swimlanes[0].cards.single.id, 'card-1');
      expect(
        failingViewModel.swimlanes[1].cards.map((c) => c.id),
        isNot(contains('card-1')),
      );
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test('rescheduleCard sets the start and end date', () async {
    final card = viewModel.swimlanes[0].cards.single;
    final start = DateTime(2026, 2, 1);
    final end = DateTime(2026, 2, 7);

    await viewModel.rescheduleCard(card, start, end);

    final updated = viewModel.swimlanes[0].cards.single;
    expect(updated.startDate, start);
    expect(updated.endDate, end);
  });

  test('rescheduleCard persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;
    final start = DateTime(2026, 2, 1);

    await viewModel.rescheduleCard(card, start, null);

    expect(repository.reschedules, ['card-1->$start..null']);
  });

  test(
    'rescheduleCard rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        rescheduleError: Exception('boom'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.rescheduleCard(card, DateTime(2026, 3, 1), null);

      expect(failingViewModel.swimlanes[0].cards.single.startDate, isNull);
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test("assign sets a card's assignee", () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.assign(card.id, 'user-1');

    expect(viewModel.swimlanes[0].cards.single.assignedToUserId, 'user-1');
  });

  test(
    "assign sets a swimlane's own assignee when the id is its parent item",
    () async {
      await viewModel.assign('lane-a', 'user-1');

      expect(viewModel.swimlanes[0].assignedToUserId, 'user-1');
      // The lane's cards are untouched — only the lane's own assignee changed.
      expect(viewModel.swimlanes[0].cards.single.assignedToUserId, isNull);
    },
  );

  test('assign clears an assignee when passed null', () async {
    final card = viewModel.swimlanes[0].cards.single;
    await viewModel.assign(card.id, 'user-1');

    await viewModel.assign(card.id, null);

    expect(viewModel.swimlanes[0].cards.single.assignedToUserId, isNull);
  });

  test('assign persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.assign(card.id, 'user-1');

    expect(repository.assigns, ['card-1->user-1']);
  });

  test('assign updates a matching Hierarchy item too', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();

    await hierarchyViewModel.assign('root-1', 'user-1');

    expect(
      hierarchyViewModel.hierarchyRoots.single.item.assignedToUserId,
      'user-1',
    );
  });

  test(
    'assign rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        assignError: Exception('boom'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.assign(card.id, 'user-1');

      expect(
        failingViewModel.swimlanes[0].cards.single.assignedToUserId,
        isNull,
      );
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test('setTags updates the card locally', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.setTags(card.id, ['urgent', 'needs review']);

    expect(viewModel.swimlanes[0].cards.single.tags, [
      'urgent',
      'needs review',
    ]);
  });

  test('setTags persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.setTags(card.id, ['urgent']);

    expect(repository.tagUpdates, ['card-1->urgent']);
  });

  test('setTags updates a matching Hierarchy item too', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();

    await hierarchyViewModel.setTags('root-1', ['urgent']);

    expect(hierarchyViewModel.hierarchyRoots.single.item.tags, ['urgent']);
  });

  test(
    'setTags rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        tagsError: Exception('boom'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.setTags(card.id, ['urgent']);

      expect(failingViewModel.swimlanes[0].cards.single.tags, isEmpty);
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test(
    'createWorkItem persists the new item and reloads the current scope',
    () async {
      await viewModel.createWorkItem(
        title: 'New card',
        parentId: 'lane-a',
        statusId: 'todo',
      );

      expect(repository.creates, ['New card->lane-a/todo']);
      expect(repository.requestedScopes, [null, null]);
      expect(viewModel.loadError, isNull);
    },
  );

  test(
    'createWorkItem failure keeps the board and reports a transient error',
    () async {
      final failingRepository = _TestBoardRepository(
        createError: Exception('boom'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();

      await failingViewModel.createWorkItem(
        title: 'New card',
        parentId: 'lane-a',
        statusId: 'todo',
      );

      expect(failingViewModel.loadError, isNull);
      expect(failingViewModel.moveError, isNotNull);
      expect(failingViewModel.swimlanes, isNotEmpty);
    },
  );

  test(
    'retry after a successful create never creates the item again',
    () async {
      await viewModel.createWorkItem(
        title: 'New card',
        parentId: 'lane-a',
        statusId: 'todo',
      );

      await viewModel.retry();

      expect(repository.creates, ['New card->lane-a/todo']);
    },
  );

  test(
    'retry after a successful drillInto does not push the breadcrumb again',
    () async {
      await viewModel.drillInto(viewModel.swimlanes[0].cards.single);
      final crumbs = viewModel.breadcrumbs.length;

      await viewModel.retry();

      expect(viewModel.breadcrumbs, hasLength(crumbs));
    },
  );

  test(
    'refreshCurrentScope reloads the scope shown and keeps the breadcrumbs',
    () async {
      await viewModel.drillInto(viewModel.swimlanes[0].cards.single);
      repository.requestedScopes.clear();

      await viewModel.refreshCurrentScope();

      expect(repository.requestedScopes, ['card-1']);
      expect(viewModel.breadcrumbs.map((c) => c.id), [null, 'card-1']);
    },
  );

  test(
    'createWorkItem does not reload if the user navigated away while it was in flight',
    () async {
      repository.createGate = Completer<void>();
      final create = viewModel.createWorkItem(
        title: 'New card',
        parentId: 'lane-a',
        statusId: 'todo',
      );
      await viewModel.drillInto(viewModel.swimlanes[0].cards.single);
      repository.requestedScopes.clear();

      repository.createGate!.complete();
      await create;

      expect(repository.requestedScopes, isEmpty);
    },
  );

  test(
    'canCreateWorkItem is false while loading and after a failed load',
    () async {
      final failingViewModel = BoardViewModel(_FailingLoadRepository());
      expect(failingViewModel.canCreateWorkItem, isFalse);

      await failingViewModel.load();

      expect(failingViewModel.canCreateWorkItem, isFalse);
      expect(viewModel.canCreateWorkItem, isTrue);
    },
  );

  test('matchesTimeFilter is true for every card when no filter is set', () {
    const card = WorkItemCard(
      id: 'x',
      number: 4,
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesTimeFilter(card), isTrue);
  });

  test(
    "matchesTimeFilter is true when the card's window overlaps the filter",
    () {
      viewModel.setTimeFilter(
        start: DateTime(2026, 1, 10),
        end: DateTime(2026, 1, 20),
      );
      final card = WorkItemCard(
        id: 'x',
        number: 5,
        title: 'X',
        parentId: 'lane-a',
        statusId: 'todo',
        startDate: DateTime(2026, 1, 15),
        endDate: DateTime(2026, 1, 16),
      );

      expect(viewModel.matchesTimeFilter(card), isTrue);
    },
  );

  test(
    "matchesTimeFilter is false when the card's window is entirely before the filter",
    () {
      viewModel.setTimeFilter(
        start: DateTime(2026, 1, 10),
        end: DateTime(2026, 1, 20),
      );
      final card = WorkItemCard(
        id: 'x',
        number: 6,
        title: 'X',
        parentId: 'lane-a',
        statusId: 'todo',
        startDate: DateTime(2026, 1, 1),
        endDate: DateTime(2026, 1, 5),
      );

      expect(viewModel.matchesTimeFilter(card), isFalse);
    },
  );

  test('matchesTimeFilter treats a missing start as open toward the past', () {
    viewModel.setTimeFilter(
      start: DateTime(2026, 1, 1),
      end: DateTime(2026, 1, 5),
    );
    final card = WorkItemCard(
      id: 'x',
      number: 7,
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
      endDate: DateTime(2026, 1, 3),
    );

    expect(viewModel.matchesTimeFilter(card), isTrue);
  });

  test('matchesTimeFilter treats a missing end as open toward the future', () {
    viewModel.setTimeFilter(
      start: DateTime(2026, 1, 10),
      end: DateTime(2026, 1, 20),
    );
    final card = WorkItemCard(
      id: 'x',
      number: 8,
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: DateTime(2026, 1, 12),
    );

    expect(viewModel.matchesTimeFilter(card), isTrue);
  });

  test('clearTimeFilter removes both filter bounds', () {
    viewModel
      ..setTimeFilter(start: DateTime(2026, 1, 1), end: DateTime(2026, 1, 5))
      ..clearTimeFilter();

    expect(viewModel.filterStart, isNull);
    expect(viewModel.filterEnd, isNull);
  });

  test('matchesSearch is true for every card when the query is empty', () {
    const card = WorkItemCard(
      id: 'x',
      number: 9,
      title: 'Anything',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesSearch(card), isTrue);
  });

  test('matchesSearch matches the title case-insensitively', () {
    viewModel.setSearchQuery('rEd');
    const card = WorkItemCard(
      id: 'x',
      number: 10,
      title: 'Fix red button',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesSearch(card), isTrue);
  });

  test('matchesSearch matches the description when the title does not', () {
    viewModel.setSearchQuery('migration');
    const card = WorkItemCard(
      id: 'x',
      number: 11,
      title: 'Backend work',
      description: 'Write the database migration',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesSearch(card), isTrue);
  });

  test('matchesSearch is false when neither title nor description match', () {
    viewModel.setSearchQuery('migration');
    const card = WorkItemCard(
      id: 'x',
      number: 12,
      title: 'Fix red button',
      description: 'Unrelated',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesSearch(card), isFalse);
  });

  test(
    'matchesSearch matches a tag when neither title nor description match',
    () {
      viewModel.setSearchQuery('urgent');
      const card = WorkItemCard(
        id: 'x',
        number: 15,
        title: 'Fix red button',
        description: 'Unrelated',
        parentId: 'lane-a',
        statusId: 'todo',
        tags: ['Urgent', 'design'],
      );

      expect(viewModel.matchesSearch(card), isTrue);
    },
  );

  test('clearSearchQuery resets the query', () {
    viewModel
      ..setSearchQuery('red')
      ..clearSearchQuery();

    expect(viewModel.searchQuery, isEmpty);
  });

  test(
    'cardVisible requires both the time filter and the search query to match',
    () {
      viewModel
        ..setTimeFilter(
          start: DateTime(2026, 1, 10),
          end: DateTime(2026, 1, 20),
        )
        ..setSearchQuery('red');
      const inWindowWrongTitle = WorkItemCard(
        id: 'a',
        number: 13,
        title: 'Blue button',
        parentId: 'lane-a',
        statusId: 'todo',
        startDate: null,
        endDate: null,
      );
      const outOfWindowRightTitle = WorkItemCard(
        id: 'b',
        number: 14,
        title: 'Red button',
        parentId: 'lane-a',
        statusId: 'todo',
        startDate: null,
        endDate: null,
      );

      expect(viewModel.cardVisible(inWindowWrongTitle), isFalse);
      expect(viewModel.cardVisible(outOfWindowRightTitle), isTrue);
    },
  );

  test('statusColorFor returns the matching status color', () {
    expect(viewModel.statusColorFor('todo'), _todoColor);
  });

  test('statusColorFor returns null for an unknown status', () {
    expect(viewModel.statusColorFor('unknown'), isNull);
  });

  test(
    'assigneeInitialFor returns the uppercased first letter of the matching username',
    () async {
      final withUsers = BoardViewModel(
        _TestBoardRepository(
          users: const [
            AuthUser(id: 'user-1', username: 'riley', kind: UserKind.human),
          ],
        ),
      );
      await withUsers.load();

      expect(withUsers.assigneeInitialFor('user-1'), 'R');
    },
  );

  test('assigneeInitialFor returns null for a null user id', () {
    expect(viewModel.assigneeInitialFor(null), isNull);
  });

  test('assigneeInitialFor returns null when no user matches the id', () {
    expect(viewModel.assigneeInitialFor('unknown-user'), isNull);
  });

  test('toggleStatusVisibility hides and then re-shows a status', () {
    viewModel.toggleStatusVisibility('done');
    expect(viewModel.visibleStatuses.map((s) => s.id), ['todo']);
    expect(viewModel.hiddenStatusIds, {'done'});

    viewModel.toggleStatusVisibility('done');
    expect(viewModel.visibleStatuses.map((s) => s.id), ['todo', 'done']);
    expect(viewModel.hiddenStatusIds, isEmpty);
  });

  test('matchesTagFilter is true for everything when no tag is selected', () {
    expect(viewModel.matchesTagFilter(const []), isTrue);
    expect(viewModel.matchesTagFilter(const ['urgent']), isTrue);
  });

  test(
    'toggleTagFilter narrows matchesTagFilter to items with a selected tag',
    () {
      viewModel.toggleTagFilter('urgent');

      expect(viewModel.matchesTagFilter(const ['urgent', 'design']), isTrue);
      expect(viewModel.matchesTagFilter(const ['design']), isFalse);
      expect(viewModel.isTagFilterSelected('urgent'), isTrue);

      viewModel.toggleTagFilter('urgent');
      expect(viewModel.matchesTagFilter(const ['design']), isTrue);
    },
  );

  test('cardVisible respects the tag filter', () {
    viewModel.toggleTagFilter('urgent');
    const tagged = WorkItemCard(
      id: 'a',
      number: 16,
      title: 'Tagged',
      parentId: 'lane-a',
      statusId: 'todo',
      tags: ['urgent'],
    );
    const untagged = WorkItemCard(
      id: 'b',
      number: 17,
      title: 'Untagged',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.cardVisible(tagged), isTrue);
    expect(viewModel.cardVisible(untagged), isFalse);
  });

  test(
    'availableTags returns the distinct, sorted union of every card and hierarchy tag',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: const [
          HierarchyItem(
            id: 'root-1',
            number: 1,
            parentId: null,
            title: 'Root',
            statusId: 'todo',
            tags: ['Zebra', 'urgent'],
          ),
        ],
      );
      final withTags = BoardViewModel(repository);
      await withTags.load();
      await withTags.loadHierarchy();
      await withTags.setTags(withTags.swimlanes[0].cards.single.id, [
        'bug',
        'Urgent',
      ]);

      expect(withTags.availableTags, ['bug', 'Urgent', 'Zebra']);
    },
  );

  test('cardComparator is null for the default manual sort', () {
    expect(viewModel.sortOption, CardSortOption.manual);
    expect(viewModel.cardComparator, isNull);
  });

  test('cardComparator for title sorts case-insensitively', () {
    viewModel.setSortOption(CardSortOption.title);
    const a = WorkItemCard(
      id: 'a',
      number: 15,
      title: 'banana',
      parentId: 'lane-a',
      statusId: 'todo',
    );
    const b = WorkItemCard(
      id: 'b',
      number: 16,
      title: 'Apple',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.cardComparator!(a, b), greaterThan(0));
  });

  test('cardComparator for start date sorts unscheduled cards last', () {
    viewModel.setSortOption(CardSortOption.startDate);
    const scheduled = WorkItemCard(
      id: 'a',
      number: 17,
      title: 'A',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: null,
    );
    final unscheduled = WorkItemCard(
      id: 'b',
      number: 18,
      title: 'B',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: DateTime(2026, 1, 1),
    );

    expect(viewModel.cardComparator!(scheduled, unscheduled), greaterThan(0));
    expect(viewModel.cardComparator!(unscheduled, scheduled), lessThan(0));
  });

  test('cardComparator for due date orders earlier dates first', () {
    viewModel.setSortOption(CardSortOption.dueDate);
    final earlier = WorkItemCard(
      id: 'a',
      number: 19,
      title: 'A',
      parentId: 'lane-a',
      statusId: 'todo',
      endDate: DateTime(2026, 1, 1),
    );
    final later = WorkItemCard(
      id: 'b',
      number: 20,
      title: 'B',
      parentId: 'lane-a',
      statusId: 'todo',
      endDate: DateTime(2026, 2, 1),
    );

    expect(viewModel.cardComparator!(earlier, later), lessThan(0));
  });

  test('loadHierarchy populates hierarchyRoots and hierarchyLoaded', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
        HierarchyItem(
          id: 'child-1',
          number: 2,
          parentId: 'root-1',
          title: 'Child',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    expect(hierarchyViewModel.hierarchyLoaded, isFalse);

    await hierarchyViewModel.loadHierarchy();

    expect(hierarchyViewModel.hierarchyLoaded, isTrue);
    expect(hierarchyViewModel.hierarchyLoadError, isNull);
    final root = hierarchyViewModel.hierarchyRoots.single;
    expect(root.item.id, 'root-1');
    expect(root.children.single.item.id, 'child-1');
  });

  test(
    'loadHierarchy sets hierarchyLoadError when the repository throws',
    () async {
      final repository = _TestBoardRepository(
        hierarchyError: Exception('network down'),
      );
      final hierarchyViewModel = BoardViewModel(repository);
      await hierarchyViewModel.load();

      await hierarchyViewModel.loadHierarchy();

      expect(hierarchyViewModel.hierarchyLoadError, isNotNull);
      expect(hierarchyViewModel.hierarchyLoaded, isTrue);
    },
  );

  test(
    'hierarchyRoots keeps an ancestor visible when only a descendant matches the search',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: const [
          HierarchyItem(
            id: 'root-1',
            number: 1,
            parentId: null,
            title: 'Unrelated root',
            statusId: 'todo',
          ),
          HierarchyItem(
            id: 'child-1',
            number: 2,
            parentId: 'root-1',
            title: 'Fix red button',
            statusId: 'todo',
          ),
        ],
      );
      final hierarchyViewModel = BoardViewModel(repository);
      await hierarchyViewModel.load();
      await hierarchyViewModel.loadHierarchy();
      hierarchyViewModel.setSearchQuery('red');

      final root = hierarchyViewModel.hierarchyRoots.single;
      expect(root.item.id, 'root-1');
      expect(root.children.single.item.id, 'child-1');
    },
  );

  test('hierarchyRoots excludes a subtree with no matching item', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Matches',
          statusId: 'todo',
        ),
        HierarchyItem(
          id: 'root-2',
          number: 2,
          parentId: null,
          title: 'Does not',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.setSearchQuery('matches');

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['root-1']);
  });

  test('hierarchyRoots excludes items in a hidden status column', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Todo item',
          statusId: 'todo',
        ),
        HierarchyItem(
          id: 'root-2',
          number: 2,
          parentId: null,
          title: 'Done item',
          statusId: 'done',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.toggleStatusVisibility('done');

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['root-1']);
  });

  test('hierarchyRoots excludes items not matching the tag filter', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(
          id: 'root-1',
          number: 1,
          parentId: null,
          title: 'Tagged',
          statusId: 'todo',
          tags: ['urgent'],
        ),
        HierarchyItem(
          id: 'root-2',
          number: 2,
          parentId: null,
          title: 'Untagged',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.toggleTagFilter('urgent');

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['root-1']);
  });

  test(
    'hierarchyComparator for title sorts siblings case-insensitively',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: const [
          HierarchyItem(
            id: 'a',
            number: 1,
            parentId: null,
            title: 'banana',
            statusId: 'todo',
          ),
          HierarchyItem(
            id: 'b',
            number: 2,
            parentId: null,
            title: 'Apple',
            statusId: 'todo',
          ),
        ],
      );
      final hierarchyViewModel = BoardViewModel(repository);
      await hierarchyViewModel.load();
      await hierarchyViewModel.loadHierarchy();
      hierarchyViewModel.setSortOption(CardSortOption.title);

      expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), [
        'b',
        'a',
      ]);
    },
  );

  test(
    'refreshHierarchyIfLoaded is a no-op before the first loadHierarchy call',
    () async {
      expect(repository.loadAllItemsCallCount, 0);

      await viewModel.refreshHierarchyIfLoaded();

      expect(repository.loadAllItemsCallCount, 0);
    },
  );

  test(
    'refreshHierarchyIfLoaded reloads once hierarchy has already been loaded',
    () async {
      await viewModel.loadHierarchy();
      expect(repository.loadAllItemsCallCount, 1);

      await viewModel.refreshHierarchyIfLoaded();

      expect(repository.loadAllItemsCallCount, 2);
    },
  );

  group('overlapping async work', () {
    HierarchyItem hierarchyItem(String title) => HierarchyItem(
      id: 'x',
      number: 1,
      parentId: null,
      title: title,
      statusId: 'todo',
    );

    test(
      'an older scope load finishing last does not overwrite the newer scope',
      () async {
        repository.loadGates[null] = Completer<void>();
        final olderRefresh = viewModel.refreshCurrentScope();
        await viewModel.drillInto(viewModel.swimlanes[0].cards.single);

        repository.loadGates[null]!.complete();
        await olderRefresh;

        expect(viewModel.breadcrumbs.map((c) => c.id), [null, 'card-1']);
        expect(viewModel.swimlanes.single.parentId, 'card-1');
      },
    );

    test('the spinner stays up until the newest scope load finishes', () async {
      repository.loadGates[null] = Completer<void>();
      repository.loadGates['card-1'] = Completer<void>();
      final older = viewModel.refreshCurrentScope();
      final newer = viewModel.drillInto(viewModel.swimlanes[0].cards.single);

      repository.loadGates[null]!.complete();
      await older;
      expect(viewModel.isLoading, isTrue);

      repository.loadGates['card-1']!.complete();
      await newer;
      expect(viewModel.isLoading, isFalse);
    });

    test(
      'an older hierarchy load finishing last does not overwrite the newer one',
      () async {
        repository.pendingHierarchyLoads = [];
        final older = viewModel.loadHierarchy();
        final newer = viewModel.loadHierarchy();

        repository.pendingHierarchyLoads![1].complete([hierarchyItem('newer')]);
        await newer;
        repository.pendingHierarchyLoads![0].complete([hierarchyItem('older')]);
        await older;

        expect(viewModel.hierarchyRoots.single.item.title, 'newer');
      },
    );

    test(
      'a failed move does not undo a different move that succeeded meanwhile',
      () async {
        repository.statusGates['card-1'] = Completer<void>();
        final failing = viewModel.moveCard(
          viewModel.swimlanes[0].cards.single,
          'done',
        );
        await viewModel.moveCard(viewModel.swimlanes[1].cards.single, 'done');

        repository.statusGates['card-1']!.completeError(Exception('rejected'));
        await failing;

        expect(viewModel.swimlanes[0].cards.single.statusId, 'todo');
        expect(viewModel.swimlanes[1].cards.single.statusId, 'done');
        expect(viewModel.moveError, isNotNull);
      },
    );

    test(
      'a failed move after a scope change leaves the new scope alone',
      () async {
        repository.statusGates['card-1'] = Completer<void>();
        final failing = viewModel.moveCard(
          viewModel.swimlanes[0].cards.single,
          'done',
        );
        await viewModel.drillInto(viewModel.swimlanes[0].cards.single);

        repository.statusGates['card-1']!.completeError(Exception('rejected'));
        await failing;

        expect(viewModel.swimlanes.single.parentId, 'card-1');
        expect(viewModel.swimlanes.single.cards.single.id, 'grandchild-1');
        expect(viewModel.moveError, isNotNull);
      },
    );
  });

  group('hierarchy stays in step with board mutations', () {
    late _TestBoardRepository hierarchyRepository;
    late BoardViewModel hierarchyViewModel;

    setUp(() async {
      hierarchyRepository = _TestBoardRepository(
        hierarchyItems: const [
          HierarchyItem(
            id: 'lane-a',
            number: 10,
            parentId: null,
            title: 'Lane A',
            statusId: 'todo',
          ),
          HierarchyItem(
            id: 'lane-b',
            number: 11,
            parentId: null,
            title: 'Lane B',
            statusId: 'todo',
          ),
          HierarchyItem(
            id: 'card-1',
            number: 1,
            parentId: 'lane-a',
            title: 'Card 1',
            statusId: 'todo',
          ),
        ],
      );
      hierarchyViewModel = BoardViewModel(hierarchyRepository);
      await hierarchyViewModel.load();
      await hierarchyViewModel.loadHierarchy();
    });

    HierarchyItem card1() =>
        hierarchyViewModel.hierarchyRoots.expand((n) => n.children).single.item;

    test('moveCard also moves the Hierarchy item to the new status', () async {
      await hierarchyViewModel.moveCard(
        hierarchyViewModel.swimlanes[0].cards.single,
        'done',
      );

      expect(card1().statusId, 'done');
    });

    test(
      'reparentCard also moves the Hierarchy item under its new parent',
      () async {
        await hierarchyViewModel.reparentCard(
          hierarchyViewModel.swimlanes[0].cards.single,
          'lane-b',
        );

        final laneB = hierarchyViewModel.hierarchyRoots.singleWhere(
          (n) => n.item.id == 'lane-b',
        );
        expect(laneB.children.single.item.id, 'card-1');
      },
    );

    test('rescheduleCard also reschedules the Hierarchy item', () async {
      final start = DateTime(2026, 9, 25);

      await hierarchyViewModel.rescheduleCard(
        hierarchyViewModel.swimlanes[0].cards.single,
        start,
        null,
      );

      expect(card1().startDate, start);
    });

    test('a failed move reverts the Hierarchy item too', () async {
      final failing = _TestBoardRepository(
        changeStatusError: Exception('rejected'),
        hierarchyItems: hierarchyRepository.hierarchyItems,
      );
      hierarchyViewModel = BoardViewModel(failing);
      await hierarchyViewModel.load();
      await hierarchyViewModel.loadHierarchy();

      await hierarchyViewModel.moveCard(
        hierarchyViewModel.swimlanes[0].cards.single,
        'done',
      );

      expect(card1().statusId, 'todo');
    });
  });

  test('the tag filter ignores casing, like the merged tag chips', () {
    viewModel.toggleTagFilter('Urgent');

    expect(viewModel.matchesTagFilter(const ['urgent']), isTrue);
    expect(viewModel.matchesTagFilter(const ['URGENT', 'design']), isTrue);
    expect(viewModel.isTagFilterSelected('urgent'), isTrue);

    viewModel.toggleTagFilter('urgent');

    expect(viewModel.isTagFilterSelected('Urgent'), isFalse);
  });

  group('time filter bounds', () {
    WorkItemCard card({DateTime? start, DateTime? end}) => WorkItemCard(
      id: 'x',
      number: 1,
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: start,
      endDate: end,
    );

    test('the "to" bound includes its whole day', () {
      viewModel.setTimeFilter(end: DateTime(2026, 9, 25));

      expect(
        viewModel.matchesTimeFilter(card(start: DateTime(2026, 9, 25))),
        isTrue,
      );
      expect(
        viewModel.matchesTimeFilter(card(start: DateTime(2026, 9, 25, 18))),
        isTrue,
      );
      expect(
        viewModel.matchesTimeFilter(card(start: DateTime(2026, 9, 26))),
        isFalse,
      );
    });

    test('the "from" bound includes its whole day', () {
      viewModel.setTimeFilter(start: DateTime(2026, 9, 25, 12));

      expect(
        viewModel.matchesTimeFilter(card(end: DateTime(2026, 9, 25))),
        isTrue,
      );
      expect(
        viewModel.matchesTimeFilter(card(end: DateTime(2026, 9, 24))),
        isFalse,
      );
    });

    test('an inverted range is swapped rather than matching nothing', () {
      viewModel.setTimeFilter(
        start: DateTime(2026, 9, 30),
        end: DateTime(2026, 9, 1),
      );

      expect(viewModel.filterStart, DateTime(2026, 9, 1));
      expect(viewModel.filterEnd, DateTime(2026, 9, 30));
      expect(
        viewModel.matchesTimeFilter(card(start: DateTime(2026, 9, 15))),
        isTrue,
      );
    });
  });
}
