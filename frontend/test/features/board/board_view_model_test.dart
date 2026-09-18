import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

const _rootBoard = BoardData(
  statuses: [
    BoardStatus(id: 'done', name: 'Done', order: 1),
    BoardStatus(id: 'todo', name: 'To Do', order: 0),
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
  });

  final Exception? changeStatusError;
  final Exception? reparentError;
  final Exception? rescheduleError;
  final List<String> statusChanges = [];
  final List<String> reparents = [];
  final List<String> reschedules = [];
  final List<String?> requestedScopes = [];

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
}

class _FailingLoadRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() =>
      Future.error(Exception('network down'));

  @override
  Future<BoardData> loadBoard(String? scopeItemId) =>
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
}
