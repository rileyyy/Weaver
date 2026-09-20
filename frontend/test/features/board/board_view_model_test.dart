import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
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
    this.hierarchyItems = const [],
    this.hierarchyError,
  });

  final Exception? changeStatusError;
  final Exception? reparentError;
  final Exception? rescheduleError;
  final Exception? createError;
  final List<HierarchyItem> hierarchyItems;
  final Exception? hierarchyError;
  final List<String> statusChanges = [];
  final List<String> reparents = [];
  final List<String> reschedules = [];
  final List<String?> requestedScopes = [];
  final List<String> creates = [];
  int loadAllItemsCallCount = 0;

  @override
  Future<String?> loadRootScopeItemId() => Future.value();

  @override
  Future<BoardData> loadBoard(String? scopeItemId) {
    requestedScopes.add(scopeItemId);
    if (scopeItemId == null) return Future.value(_rootBoard);
    if (scopeItemId == 'card-1') return Future.value(_card1Children);
    return Future.error(StateError('No fixture for scope $scopeItemId'));
  }

  @override
  Future<List<HierarchyItem>> loadAllItems() async {
    loadAllItemsCallCount++;
    final error = hierarchyError;
    if (error != null) throw error;
    return hierarchyItems;
  }

  @override
  Future<void> changeStatus(String cardId, String newStatusId) async {
    statusChanges.add('$cardId->$newStatusId');
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
    final error = createError;
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

  test('drillInto sets loadError and leaves the breadcrumb unchanged on failure', () async {
    final cardWithNoFixture = viewModel.swimlanes[1].cards.single;

    await viewModel.drillInto(cardWithNoFixture);

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.breadcrumbs, hasLength(1));
  });

  test('navigateToBreadcrumb returns to a previous scope', () async {
    final card = viewModel.swimlanes[0].cards.single;
    await viewModel.drillInto(card);

    await viewModel.navigateToBreadcrumb(0);

    expect(viewModel.breadcrumbs, hasLength(1));
    expect(viewModel.swimlanes.any((lane) => lane.parentId == 'lane-a'), isTrue);
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
      final movedCard = viewModel.swimlanes[1].cards
          .firstWhere((c) => c.id == 'card-1');
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

  test('createWorkItem persists the new item and reloads the current scope', () async {
    await viewModel.createWorkItem(
      title: 'New card',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(repository.creates, ['New card->lane-a/todo']);
    expect(repository.requestedScopes, [null, null]);
    expect(viewModel.loadError, isNull);
  });

  test('createWorkItem sets loadError when the repository call fails', () async {
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

    expect(failingViewModel.loadError, isNotNull);
  });

  test('matchesTimeFilter is true for every card when no filter is set', () {
    const card = WorkItemCard(
      id: 'x',
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesTimeFilter(card), isTrue);
  });

  test("matchesTimeFilter is true when the card's window overlaps the filter", () {
    viewModel.setTimeFilter(
      start: DateTime(2026, 1, 10),
      end: DateTime(2026, 1, 20),
    );
    final card = WorkItemCard(
      id: 'x',
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: DateTime(2026, 1, 15),
      endDate: DateTime(2026, 1, 16),
    );

    expect(viewModel.matchesTimeFilter(card), isTrue);
  });

  test("matchesTimeFilter is false when the card's window is entirely before the filter", () {
    viewModel.setTimeFilter(
      start: DateTime(2026, 1, 10),
      end: DateTime(2026, 1, 20),
    );
    final card = WorkItemCard(
      id: 'x',
      title: 'X',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: DateTime(2026, 1, 1),
      endDate: DateTime(2026, 1, 5),
    );

    expect(viewModel.matchesTimeFilter(card), isFalse);
  });

  test('matchesTimeFilter treats a missing start as open toward the past', () {
    viewModel.setTimeFilter(
      start: DateTime(2026, 1, 1),
      end: DateTime(2026, 1, 5),
    );
    final card = WorkItemCard(
      id: 'x',
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
      title: 'Fix red button',
      description: 'Unrelated',
      parentId: 'lane-a',
      statusId: 'todo',
    );

    expect(viewModel.matchesSearch(card), isFalse);
  });

  test('clearSearchQuery resets the query', () {
    viewModel
      ..setSearchQuery('red')
      ..clearSearchQuery();

    expect(viewModel.searchQuery, isEmpty);
  });

  test('cardVisible requires both the time filter and the search query to match', () {
    viewModel
      ..setTimeFilter(start: DateTime(2026, 1, 10), end: DateTime(2026, 1, 20))
      ..setSearchQuery('red');
    const inWindowWrongTitle = WorkItemCard(
      id: 'a',
      title: 'Blue button',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: null,
      endDate: null,
    );
    const outOfWindowRightTitle = WorkItemCard(
      id: 'b',
      title: 'Red button',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: null,
      endDate: null,
    );

    expect(viewModel.cardVisible(inWindowWrongTitle), isFalse);
    expect(viewModel.cardVisible(outOfWindowRightTitle), isTrue);
  });

  test('statusColorFor returns the matching status color', () {
    expect(viewModel.statusColorFor('todo'), _todoColor);
  });

  test('statusColorFor returns null for an unknown status', () {
    expect(viewModel.statusColorFor('unknown'), isNull);
  });

  test('toggleStatusVisibility hides and then re-shows a status', () {
    viewModel.toggleStatusVisibility('done');
    expect(viewModel.visibleStatuses.map((s) => s.id), ['todo']);
    expect(viewModel.hiddenStatusIds, {'done'});

    viewModel.toggleStatusVisibility('done');
    expect(viewModel.visibleStatuses.map((s) => s.id), ['todo', 'done']);
    expect(viewModel.hiddenStatusIds, isEmpty);
  });

  test('cardComparator is null for the default manual sort', () {
    expect(viewModel.sortOption, CardSortOption.manual);
    expect(viewModel.cardComparator, isNull);
  });

  test('cardComparator for title sorts case-insensitively', () {
    viewModel.setSortOption(CardSortOption.title);
    const a = WorkItemCard(
      id: 'a',
      title: 'banana',
      parentId: 'lane-a',
      statusId: 'todo',
    );
    const b = WorkItemCard(
      id: 'b',
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
      title: 'A',
      parentId: 'lane-a',
      statusId: 'todo',
      startDate: null,
    );
    final unscheduled = WorkItemCard(
      id: 'b',
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
      title: 'A',
      parentId: 'lane-a',
      statusId: 'todo',
      endDate: DateTime(2026, 1, 1),
    );
    final later = WorkItemCard(
      id: 'b',
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
        HierarchyItem(id: 'root-1', parentId: null, title: 'Root', statusId: 'todo'),
        HierarchyItem(id: 'child-1', parentId: 'root-1', title: 'Child', statusId: 'todo'),
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

  test('loadHierarchy sets hierarchyLoadError when the repository throws', () async {
    final repository = _TestBoardRepository(hierarchyError: Exception('network down'));
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();

    await hierarchyViewModel.loadHierarchy();

    expect(hierarchyViewModel.hierarchyLoadError, isNotNull);
    expect(hierarchyViewModel.hierarchyLoaded, isTrue);
  });

  test(
    'hierarchyRoots keeps an ancestor visible when only a descendant matches the search',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: const [
          HierarchyItem(id: 'root-1', parentId: null, title: 'Unrelated root', statusId: 'todo'),
          HierarchyItem(id: 'child-1', parentId: 'root-1', title: 'Fix red button', statusId: 'todo'),
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
        HierarchyItem(id: 'root-1', parentId: null, title: 'Matches', statusId: 'todo'),
        HierarchyItem(id: 'root-2', parentId: null, title: 'Does not', statusId: 'todo'),
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
        HierarchyItem(id: 'root-1', parentId: null, title: 'Todo item', statusId: 'todo'),
        HierarchyItem(id: 'root-2', parentId: null, title: 'Done item', statusId: 'done'),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.toggleStatusVisibility('done');

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['root-1']);
  });

  test('hierarchyComparator for title sorts siblings case-insensitively', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: const [
        HierarchyItem(id: 'a', parentId: null, title: 'banana', statusId: 'todo'),
        HierarchyItem(id: 'b', parentId: null, title: 'Apple', statusId: 'todo'),
      ],
    );
    final hierarchyViewModel = BoardViewModel(repository);
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.setSortOption(CardSortOption.title);

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['b', 'a']);
  });

  test('refreshHierarchyIfLoaded is a no-op before the first loadHierarchy call', () async {
    expect(repository.loadAllItemsCallCount, 0);

    await viewModel.refreshHierarchyIfLoaded();

    expect(repository.loadAllItemsCallCount, 0);
  });

  test('refreshHierarchyIfLoaded reloads once hierarchy has already been loaded', () async {
    await viewModel.loadHierarchy();
    expect(repository.loadAllItemsCallCount, 1);

    await viewModel.refreshHierarchyIfLoaded();

    expect(repository.loadAllItemsCallCount, 2);
  });
}
