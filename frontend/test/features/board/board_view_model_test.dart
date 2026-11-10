import 'dart:async';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

import '../../features/board/fakes/fake_view_repository.dart';
import '../../shared/fakes/work_items.dart';

const _todoColor = Color(0xFF1E88E5);

const _home = Space(id: 'home', name: 'Home');

const _taskColor = Color(0xFF43A047);

final _rootBoard = BoardData(
  types: const [
    WorkItemType(id: 'task', name: 'Task', order: 0, color: _taskColor),
  ],
  statuses: const [
    WorkItemStatus(id: 'done', name: 'Done', order: 1),
    WorkItemStatus(id: 'todo', name: 'To Do', order: 0, color: _todoColor),
  ],
  swimlanes: [
    SwimlaneItems(
      lane: workItem('lane-a', title: 'Lane A'),
      cards: [
        workItem(
          'card-1',
          number: 1,
          title: 'Card 1',
          parentId: 'lane-a',
          statusId: 'todo',
        ),
      ],
    ),
    SwimlaneItems(
      lane: workItem('lane-b', title: 'Lane B'),
      cards: [
        workItem(
          'card-2',
          number: 2,
          title: 'Card 2',
          parentId: 'lane-b',
          statusId: 'todo',
        ),
      ],
    ),
  ],
);

WorkItem _laneACard(String id, String statusId) =>
    workItem(id, number: 1, title: id, parentId: 'lane-a', statusId: statusId);

/// One lane whose To Do cell holds t1, t2, t3 and whose Done cell holds
/// d1, d2, in rank order.
final _reorderBoard = BoardData(
  statuses: const [
    WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
    WorkItemStatus(id: 'done', name: 'Done', order: 1),
  ],
  swimlanes: [
    SwimlaneItems(
      lane: workItem('lane-a', title: 'Lane A'),
      cards: [
        _laneACard('t1', 'todo'),
        _laneACard('d1', 'done'),
        _laneACard('t2', 'todo'),
        _laneACard('t3', 'todo'),
        _laneACard('d2', 'done'),
      ],
    ),
  ],
);

final _card1Children = BoardData(
  statuses: const [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
  swimlanes: [
    SwimlaneItems(
      lane: workItem('card-1', title: 'Card 1'),
      cards: [
        workItem(
          'grandchild-1',
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
///
/// It keeps a "server" copy of every fixture item. Writes check the
/// `expectedVersion` against it like the real API (409 when stale), apply
/// the change and bump the version; loads return the server copies.
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
    BoardData? rootBoard,
  }) : rootBoard = rootBoard ?? _rootBoard {
    for (final board in [this.rootBoard, _card1Children]) {
      for (final lane in board.swimlanes) {
        server[lane.lane.id] ??= lane.lane;
        for (final card in lane.cards) {
          server[card.id] ??= card;
        }
      }
    }
    for (final item in hierarchyItems) {
      server[item.id] ??= item;
    }
  }

  final BoardData rootBoard;

  final Exception? changeStatusError;
  final Exception? reparentError;
  final Exception? rescheduleError;
  final Exception? createError;
  final Exception? assignError;
  final Exception? tagsError;
  final List<WorkItem> hierarchyItems;
  final Exception? hierarchyError;
  List<User> users;

  /// The server's copy of every item, by id.
  final Map<String, WorkItem> server = {};

  /// The `expectedVersion` each write was sent with, in order, as
  /// `'<id>@<version>'`.
  final List<String> sentVersions = [];

  final List<String> statusChanges = [];

  /// The `afterId` sent with each of [statusChanges], in the same order.
  final List<String?> statusChangeAnchors = [];
  final List<String> reparents = [];
  final List<String> reschedules = [];
  final List<String?> requestedScopes = [];
  final List<String> creates = [];
  final List<String> itemLoads = [];

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
  List<Completer<List<WorkItem>>>? pendingHierarchyLoads;
  final List<String> assigns = [];
  final List<String> tagUpdates = [];
  int loadAllItemsCallCount = 0;

  /// Someone else changes [id] on the server, moving its version on.
  void editElsewhere(String id, WorkItem Function(WorkItem item) edit) =>
      server[id] = withVersion(edit(server[id]!), server[id]!.version + 1);

  @override
  Future<List<Space>> loadSpaces() async => const [_home];

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async {
    requestedScopes.add(scopeItemId);
    await loadGates[scopeItemId]?.future;
    if (scopeItemId == null) return _fromServer(rootBoard);
    if (scopeItemId == 'card-1') return _fromServer(_card1Children);
    throw ApiException('No fixture for scope $scopeItemId', statusCode: 404);
  }

  BoardData _fromServer(BoardData board) => BoardData(
    statuses: board.statuses,
    types: board.types,
    swimlanes: [
      for (final lane in board.swimlanes)
        SwimlaneItems(
          lane: server[lane.lane.id]!,
          cards: [for (final card in lane.cards) server[card.id]!],
        ),
    ],
  );

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) async {
    loadAllItemsCallCount++;
    final pending = pendingHierarchyLoads;
    if (pending != null) {
      final load = Completer<List<WorkItem>>();
      pending.add(load);
      return load.future;
    }
    final error = hierarchyError;
    if (error != null) throw error;
    return [for (final item in hierarchyItems) server[item.id]!];
  }

  /// The Space's custom fields, which the Hierarchy sums for its roll-ups.
  List<CustomField> spaceFields = const [];

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) async =>
      spaceFields;

  /// Every query sent, in order.
  final List<WorkItemQuery> queries = [];

  /// Which items the "server" says match any query; null for every item
  /// it holds, as when the default done-hiding query has nothing to hide.
  Set<String>? matchingIds;

  /// When set, every query fails with it.
  Exception? queryError;

  /// When non-null, each [queryItems] call parks a completer here instead
  /// of answering at once, so a test can finish them in any order.
  List<Completer<List<WorkItem>>>? pendingQueries;

  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async {
    queries.add(query);
    final pending = pendingQueries;
    if (pending != null) {
      final answer = Completer<List<WorkItem>>();
      pending.add(answer);
      return answer.future;
    }
    final error = queryError;
    if (error != null) throw error;
    return [for (final id in matchingIds ?? server.keys) server[id]!];
  }

  @override
  Future<WorkItem> getItem(String id) async {
    itemLoads.add(id);
    return server[id]!;
  }

  @override
  Future<List<User>> loadUsers() => Future.value(users);

  Future<WorkItem> _write(
    String id,
    int expectedVersion,
    Exception? error,
    WorkItem Function(WorkItem item) change,
  ) async {
    sentVersions.add('$id@$expectedVersion');
    if (error != null) throw error;
    final current = server[id]!;
    if (current.version != expectedVersion) {
      throw const ApiException('Changed by someone else', statusCode: 409);
    }
    return server[id] = withVersion(change(current), current.version + 1);
  }

  @override
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  }) async {
    statusChanges.add('$cardId->$newStatusId');
    statusChangeAnchors.add(afterId);
    await statusGates[cardId]?.future;
    return _write(
      cardId,
      expectedVersion,
      changeStatusError,
      (item) => item.withStatus(newStatusId),
    );
  }

  @override
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
    required int expectedVersion,
  }) {
    reparents.add('$itemId->$newParentId');
    return _write(
      itemId,
      expectedVersion,
      reparentError,
      (item) => item.movedToParent(newParentId),
    );
  }

  @override
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) {
    reschedules.add('$itemId->$startDate..$endDate');
    return _write(
      itemId,
      expectedVersion,
      rescheduleError,
      (item) => item.rescheduled(startDate, endDate),
    );
  }

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String spaceId,
    required String? parentId,
    required String statusId,
  }) async {
    creates.add('$title->$parentId/$statusId');
    await createGate?.future;
    final error = createError;
    if (error != null) throw error;
  }

  @override
  Future<WorkItem> assign(
    String workItemId,
    String? userId, {
    required int expectedVersion,
  }) {
    assigns.add('$workItemId->$userId');
    return _write(
      workItemId,
      expectedVersion,
      assignError,
      (item) => item.assigned(userId),
    );
  }

  @override
  Future<WorkItem> setTags(
    String workItemId,
    List<String> tags, {
    required int expectedVersion,
  }) {
    tagUpdates.add('$workItemId->${tags.join(',')}');
    return _write(
      workItemId,
      expectedVersion,
      tagsError,
      (item) => item.tagged(tags),
    );
  }
}

class _FailingLoadRepository implements BoardRepository {
  static const _down = ApiException('network down');

  @override
  Future<List<Space>> loadSpaces() => Future.error(_down);

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) =>
      Future.error(_down);

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) => Future.error(_down);

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) =>
      Future.error(_down);

  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) => Future.error(_down);

  @override
  Future<WorkItem> getItem(String id) => Future.error(_down);

  @override
  Future<List<User>> loadUsers() => Future.value(const []);

  @override
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  }) => Future.error(_down);

  @override
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
    required int expectedVersion,
  }) => Future.error(_down);

  @override
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) => Future.error(_down);

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String spaceId,
    required String? parentId,
    required String statusId,
  }) => Future.value();

  @override
  Future<WorkItem> assign(
    String workItemId,
    String? userId, {
    required int expectedVersion,
  }) => Future.error(_down);

  @override
  Future<WorkItem> setTags(
    String workItemId,
    List<String> tags, {
    required int expectedVersion,
  }) => Future.error(_down);
}

void main() {
  late _TestBoardRepository repository;
  late WorkItemStore items;
  late BoardViewModel viewModel;

  setUp(() async {
    repository = _TestBoardRepository();
    items = WorkItemStore();
    viewModel = BoardViewModel(repository, items, FakeViewRepository());
    await viewModel.load();
  });

  test('load sorts statuses by order', () {
    expect(viewModel.statuses.map((s) => s.id), ['todo', 'done']);
  });

  test('typeColorFor resolves a loaded type and nothing else', () {
    expect(viewModel.typeColorFor('task'), _taskColor);
    expect(viewModel.typeColorFor('unknown'), isNull);
    expect(viewModel.typeColorFor(null), isNull);
  });

  test('load sets the root breadcrumb', () {
    expect(viewModel.breadcrumbs, hasLength(1));
    expect(viewModel.breadcrumbs.single.title, 'Home');
  });

  test('load sets loadError when the repository throws', () async {
    final failingViewModel = BoardViewModel(
      _FailingLoadRepository(),
      WorkItemStore(),
      FakeViewRepository(),
    );
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
        changeStatusError: const ApiException('conflict'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
    expect(viewModel.breadcrumbs.map((b) => b.title), ['Home', 'Card 1']);
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

  test('drillDownTargets offers the lanes of the current scope', () {
    expect(viewModel.drillDownTargets.map((t) => (t.id, t.title)), [
      ('lane-a', 'Lane A'),
      ('lane-b', 'Lane B'),
    ]);
  });

  test('drillIntoScope makes the target the top level of the board', () async {
    await viewModel.drillIntoScope(
      const ScopeCrumb(id: 'card-1', title: 'Card 1'),
    );

    expect(repository.requestedScopes.last, 'card-1');
    expect(viewModel.swimlanes.single.parentId, 'card-1');
    expect(viewModel.breadcrumbs.map((b) => b.id), [null, 'card-1']);
    expect(viewModel.drillDownTargets.single.id, 'card-1');
  });

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
        reparentError: const ApiException('cycle'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
        rescheduleError: const ApiException('boom'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
      hierarchyItems: [
        workItem(
          'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(
      repository,
      WorkItemStore(),
      FakeViewRepository(),
    );
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
        assignError: const ApiException('boom'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
      hierarchyItems: [
        workItem(
          'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(
      repository,
      WorkItemStore(),
      FakeViewRepository(),
    );
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();

    await hierarchyViewModel.setTags('root-1', ['urgent']);

    expect(hierarchyViewModel.hierarchyRoots.single.item.tags, ['urgent']);
  });

  test(
    'setTags rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        tagsError: const ApiException('boom'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.setTags(card.id, ['urgent']);

      expect(failingViewModel.swimlanes[0].cards.single.tags, isEmpty);
      expect(failingViewModel.moveError, isNotNull);
    },
  );

  test('addTags appends only the tags the card does not have yet', () async {
    final card = viewModel.swimlanes[0].cards.single;
    await viewModel.setTags(card.id, ['Urgent']);

    await viewModel.addTags(card.id, ['urgent', ' design ', '']);

    expect(viewModel.swimlanes[0].cards.single.tags, ['Urgent', 'design']);
    expect(repository.tagUpdates.last, 'card-1->Urgent,design');
  });

  test('addTags sends nothing when every tag is already present', () async {
    final card = viewModel.swimlanes[0].cards.single;
    await viewModel.setTags(card.id, ['urgent']);
    final updatesBefore = repository.tagUpdates.length;

    await viewModel.addTags(card.id, ['URGENT', '  ']);

    expect(repository.tagUpdates, hasLength(updatesBefore));
  });

  test('addTags adds to a Hierarchy item that is not on the board', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: [
        workItem(
          'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
          tags: ['bug'],
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(
      repository,
      WorkItemStore(),
      FakeViewRepository(),
    );
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();

    await hierarchyViewModel.addTags('root-1', ['design']);

    expect(hierarchyViewModel.hierarchyRoots.single.item.tags, [
      'bug',
      'design',
    ]);
    expect(repository.tagUpdates, ['root-1->bug,design']);
  });

  test('addTags rolls back and reports an error when the save fails', () async {
    final failingRepository = _TestBoardRepository(
      tagsError: const ApiException('boom'),
    );
    final failingViewModel = BoardViewModel(
      failingRepository,
      WorkItemStore(),
      FakeViewRepository(),
    );
    await failingViewModel.load();
    final card = failingViewModel.swimlanes[0].cards.single;

    await failingViewModel.addTags(card.id, ['urgent']);

    expect(failingViewModel.swimlanes[0].cards.single.tags, isEmpty);
    expect(failingViewModel.moveError, isNotNull);
  });

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
        createError: const ApiException('boom'),
      );
      final failingViewModel = BoardViewModel(
        failingRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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

  test('createWorkItem skips the reload once the board is disposed', () async {
    repository.createGate = Completer<void>();
    final create = viewModel.createWorkItem(
      title: 'New card',
      parentId: 'lane-a',
      statusId: 'todo',
    );
    viewModel.dispose();
    repository.requestedScopes.clear();

    repository.createGate!.complete();
    await create;

    expect(repository.requestedScopes, isEmpty);
  });

  test(
    'canCreateWorkItem is false while loading and after a failed load',
    () async {
      final failingViewModel = BoardViewModel(
        _FailingLoadRepository(),
        WorkItemStore(),
        FakeViewRepository(),
      );
      expect(failingViewModel.canCreateWorkItem, isFalse);

      await failingViewModel.load();

      expect(failingViewModel.canCreateWorkItem, isFalse);
      expect(viewModel.canCreateWorkItem, isTrue);
    },
  );

  test('clearTimeFilter removes both filter bounds', () {
    viewModel
      ..setTimeFilter(start: DateTime(2026, 1, 1), end: DateTime(2026, 1, 5))
      ..clearTimeFilter();

    expect(viewModel.filterStart, isNull);
    expect(viewModel.filterEnd, isNull);
  });

  test('clearSearchQuery resets the query', () {
    viewModel
      ..setSearchQuery('red')
      ..clearSearchQuery();

    expect(viewModel.searchQuery, isEmpty);
  });

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
            User(id: 'user-1', username: 'riley', kind: UserKind.human),
          ],
        ),
        WorkItemStore(),
        FakeViewRepository(),
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

  test(
    'availableTags returns the distinct, sorted union of every card and hierarchy tag',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: [
          workItem(
            'root-1',
            number: 1,
            parentId: null,
            title: 'Root',
            statusId: 'todo',
            tags: ['Zebra', 'urgent'],
          ),
        ],
      );
      final withTags = BoardViewModel(
        repository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
      hierarchyItems: [
        workItem(
          'root-1',
          number: 1,
          parentId: null,
          title: 'Root',
          statusId: 'todo',
        ),
        workItem(
          'child-1',
          number: 2,
          parentId: 'root-1',
          title: 'Child',
          statusId: 'todo',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(
      repository,
      WorkItemStore(),
      FakeViewRepository(),
    );
    await hierarchyViewModel.load();
    expect(hierarchyViewModel.hierarchyLoaded, isFalse);

    await hierarchyViewModel.loadHierarchy();

    expect(hierarchyViewModel.hierarchyLoaded, isTrue);
    expect(hierarchyViewModel.hierarchyLoadError, isNull);
    final root = hierarchyViewModel.hierarchyRoots.single;
    expect(root.item.id, 'root-1');
    expect(root.children.single.item.id, 'child-1');
  });

  test("hierarchyRollUps sums the Space's roll-up fields and follows an edit "
      'to a sub-item without a reload', () async {
    final child = workItem(
      'child-1',
      parentId: 'root-1',
      fields: const {'field-hours': 2},
    );
    final repository =
        _TestBoardRepository(hierarchyItems: [workItem('root-1'), child])
          ..spaceFields = const [
            CustomField(
              id: 'field-hours',
              typeId: 'type-task',
              name: 'Hours',
              kind: CustomFieldKind.number,
              order: 0,
              rollsUp: true,
            ),
          ];
    final store = WorkItemStore();
    final hierarchyViewModel = BoardViewModel(
      repository,
      store,
      FakeViewRepository(),
    );
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    final before = hierarchyViewModel.hierarchyRollUps
        .forItem('root-1')
        .single
        .total;

    store.putAll([
      workItem(
        'child-1',
        parentId: 'root-1',
        fields: const {'field-hours': 5},
        version: 2,
      ),
    ], asOf: store.revision);

    expect(before, 2);
    expect(
      hierarchyViewModel.hierarchyRollUps.forItem('root-1').single.total,
      5,
    );
  });

  test(
    'loadHierarchy sets hierarchyLoadError when the repository throws',
    () async {
      final repository = _TestBoardRepository(
        hierarchyError: const ApiException('network down'),
      );
      final hierarchyViewModel = BoardViewModel(
        repository,
        WorkItemStore(),
        FakeViewRepository(),
      );
      await hierarchyViewModel.load();

      await hierarchyViewModel.loadHierarchy();

      expect(hierarchyViewModel.hierarchyLoadError, isNotNull);
      expect(hierarchyViewModel.hierarchyLoaded, isTrue);
    },
  );

  test('hierarchyRoots excludes items in a hidden status column', () async {
    final repository = _TestBoardRepository(
      hierarchyItems: [
        workItem(
          'root-1',
          number: 1,
          parentId: null,
          title: 'Todo item',
          statusId: 'todo',
        ),
        workItem(
          'root-2',
          number: 2,
          parentId: null,
          title: 'Done item',
          statusId: 'done',
        ),
      ],
    );
    final hierarchyViewModel = BoardViewModel(
      repository,
      WorkItemStore(),
      FakeViewRepository(),
    );
    await hierarchyViewModel.load();
    await hierarchyViewModel.loadHierarchy();
    hierarchyViewModel.toggleStatusVisibility('done');

    expect(hierarchyViewModel.hierarchyRoots.map((n) => n.item.id), ['root-1']);
  });

  test(
    'hierarchyComparator for title sorts siblings case-insensitively',
    () async {
      final repository = _TestBoardRepository(
        hierarchyItems: [
          workItem(
            'a',
            number: 1,
            parentId: null,
            title: 'banana',
            statusId: 'todo',
          ),
          workItem(
            'b',
            number: 2,
            parentId: null,
            title: 'Apple',
            statusId: 'todo',
          ),
        ],
      );
      final hierarchyViewModel = BoardViewModel(
        repository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
    WorkItem hierarchyItem(String title) => workItem(
      'x',
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
        // These items exist only in the hierarchy loads, so the fake
        // server's done-hiding query would leave them out.
        viewModel.setShowDone(true);
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

        repository.statusGates['card-1']!.completeError(
          const ApiException('rejected'),
        );
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

        repository.statusGates['card-1']!.completeError(
          const ApiException('rejected'),
        );
        await failing;

        expect(viewModel.swimlanes.single.parentId, 'card-1');
        expect(viewModel.swimlanes.single.cards.single.id, 'grandchild-1');
        expect(viewModel.moveError, isNotNull);
      },
    );
  });

  group('reordering cards', () {
    late _TestBoardRepository reorderRepository;
    late BoardViewModel reorderViewModel;

    setUp(() async {
      reorderRepository = _TestBoardRepository(rootBoard: _reorderBoard);
      reorderViewModel = BoardViewModel(
        reorderRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
      await reorderViewModel.load();
    });

    WorkItemCard cardById(String id) =>
        reorderViewModel.swimlanes.single.cards.firstWhere((c) => c.id == id);

    List<String> cell(String statusId) => [
      for (final card in reorderViewModel.swimlanes.single.cards)
        if (card.statusId == statusId) card.id,
    ];

    test(
      'dropping before a card in the same cell reorders and persists it',
      () async {
        await reorderViewModel.moveCard(
          cardById('t3'),
          'todo',
          beforeCardId: 't2',
        );

        expect(cell('todo'), ['t1', 't3', 't2']);
        expect(reorderRepository.statusChanges, ['t3->todo']);
        expect(reorderRepository.statusChangeAnchors, ['t1']);
      },
    );

    test('dropping at the top of the cell sends no anchor', () async {
      await reorderViewModel.moveCard(
        cardById('t2'),
        'todo',
        beforeCardId: 't1',
      );

      expect(cell('todo'), ['t2', 't1', 't3']);
      expect(reorderRepository.statusChangeAnchors, [null]);
    });

    test('dropping a card where it already is sends nothing', () async {
      await reorderViewModel.moveCard(
        cardById('t2'),
        'todo',
        beforeCardId: 't3',
      );
      await reorderViewModel.moveCard(
        cardById('t2'),
        'todo',
        beforeCardId: 't2',
      );
      await reorderViewModel.moveCard(cardById('t3'), 'todo');

      expect(cell('todo'), ['t1', 't2', 't3']);
      expect(reorderRepository.statusChanges, isEmpty);
    });

    test(
      'moving to another status places the card at the drop position',
      () async {
        await reorderViewModel.moveCard(
          cardById('t1'),
          'done',
          beforeCardId: 'd2',
        );

        expect(cell('todo'), ['t2', 't3']);
        expect(cell('done'), ['d1', 't1', 'd2']);
        expect(reorderRepository.statusChangeAnchors, ['d1']);
      },
    );

    test(
      'moving to another status without a position appends the card',
      () async {
        await reorderViewModel.moveCard(cardById('t1'), 'done');

        expect(cell('done'), ['d1', 'd2', 't1']);
        expect(reorderRepository.statusChangeAnchors, ['d2']);
      },
    );

    test('a failed move returns the card to its old position', () async {
      final failing = _TestBoardRepository(
        rootBoard: _reorderBoard,
        changeStatusError: const ApiException('rejected'),
      );
      reorderViewModel = BoardViewModel(
        failing,
        WorkItemStore(),
        FakeViewRepository(),
      );
      await reorderViewModel.load();

      await reorderViewModel.moveCard(
        cardById('t2'),
        'done',
        beforeCardId: 'd1',
      );

      expect(cell('todo'), ['t1', 't2', 't3']);
      expect(cell('done'), ['d1', 'd2']);
      expect(reorderViewModel.moveError, isNotNull);
    });

    test('a failed reorder restores only the moved card, keeping a move that '
        'succeeded meanwhile', () async {
      reorderRepository.statusGates['t3'] = Completer<void>();
      final failing = reorderViewModel.moveCard(
        cardById('t3'),
        'todo',
        beforeCardId: 't1',
      );
      await reorderViewModel.moveCard(cardById('t2'), 'done');

      reorderRepository.statusGates['t3']!.completeError(
        const ApiException('rejected'),
      );
      await failing;

      expect(cell('todo'), ['t1', 't3']);
      expect(cell('done'), ['d1', 'd2', 't2']);
      expect(reorderViewModel.moveError, isNotNull);
    });

    test(
      'a failed reorder lands after the old neighbour that is now latest',
      () async {
        reorderRepository.statusGates['t3'] = Completer<void>();
        final failing = reorderViewModel.moveCard(
          cardById('t3'),
          'todo',
          beforeCardId: 't1',
        );
        await reorderViewModel.moveCard(cardById('t1'), 'todo');

        reorderRepository.statusGates['t3']!.completeError(
          const ApiException('rejected'),
        );
        await failing;

        expect(cell('todo'), ['t2', 't1', 't3']);
      },
    );
  });

  group('hierarchy stays in step with board mutations', () {
    late _TestBoardRepository hierarchyRepository;
    late BoardViewModel hierarchyViewModel;

    setUp(() async {
      hierarchyRepository = _TestBoardRepository(
        hierarchyItems: [
          workItem(
            'lane-a',
            number: 10,
            parentId: null,
            title: 'Lane A',
            statusId: 'todo',
          ),
          workItem(
            'lane-b',
            number: 11,
            parentId: null,
            title: 'Lane B',
            statusId: 'todo',
          ),
          workItem(
            'card-1',
            number: 1,
            parentId: 'lane-a',
            title: 'Card 1',
            statusId: 'todo',
          ),
        ],
      );
      hierarchyViewModel = BoardViewModel(
        hierarchyRepository,
        WorkItemStore(),
        FakeViewRepository(),
      );
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
        changeStatusError: const ApiException('rejected'),
        hierarchyItems: hierarchyRepository.hierarchyItems,
      );
      hierarchyViewModel = BoardViewModel(
        failing,
        WorkItemStore(),
        FakeViewRepository(),
      );
      await hierarchyViewModel.load();
      await hierarchyViewModel.loadHierarchy();

      await hierarchyViewModel.moveCard(
        hierarchyViewModel.swimlanes[0].cards.single,
        'done',
      );

      expect(card1().statusId, 'todo');
    });
  });

  group('header filters drive the server query', () {
    late _TestBoardRepository queried;
    late BoardViewModel filtered;

    setUp(() async {
      queried = _TestBoardRepository(
        hierarchyItems: [
          workItem('root-1', number: 1, parentId: null, statusId: 'todo'),
          workItem('child-1', number: 2, parentId: 'root-1', statusId: 'todo'),
          workItem('root-2', number: 3, parentId: null, statusId: 'done'),
        ],
      );
      // Showing every Done item leaves only the filters each test sets.
      filtered = BoardViewModel(
        queried,
        WorkItemStore(),
        FakeViewRepository(defaultView: allSpacesShowingDoneView),
      );
      await filtered.load();
      await filtered.loadHierarchy();
    });

    test('nothing is asked while no filter is set', () async {
      filtered
        ..toggleStatusVisibility('done')
        ..setSortOption(CardSortOption.title);
      await pumpEventQueue();

      expect(queried.queries, isEmpty);
      expect(filtered.cardVisible(filtered.swimlanes[0].cards.single), isTrue);
    });

    test('the search, time range and tags are sent together', () async {
      filtered
        ..setSearchQuery('  red  ')
        ..setTimeFilter(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        ..toggleTagFilter('Urgent');
      await pumpEventQueue();

      final query = queried.queries.last;
      expect(query.spaceIds, {'home'});
      expect(query.text, 'red');
      expect(query.from, DateTime(2026, 9, 1));
      expect(query.to, DateTime(2026, 9, 30));
      expect(query.tags, {'urgent'});
      expect(query.tagMatch, TagMatch.any);
    });

    test('choosing All sends it with the selected tags', () async {
      filtered
        ..toggleTagFilter('urgent')
        ..toggleTagFilter('design')
        ..setTagMatch(TagMatch.all);
      await pumpEventQueue();

      expect(queried.queries.last.tags, {'urgent', 'design'});
      expect(queried.queries.last.tagMatch, TagMatch.all);
    });

    test('an inverted range is sent swapped', () async {
      filtered.setTimeFilter(
        start: DateTime(2026, 9, 30),
        end: DateTime(2026, 9, 1),
      );
      await pumpEventQueue();

      expect(queried.queries.last.from, DateTime(2026, 9, 1));
      expect(queried.queries.last.to, DateTime(2026, 9, 30));
    });

    test('the board shows only the cards the server matched', () async {
      queried.matchingIds = {'card-2'};
      filtered.setSearchQuery('two');
      await pumpEventQueue();

      final card1 = filtered.swimlanes[0].cards.single;
      final card2 = filtered.swimlanes[1].cards.single;
      expect(filtered.cardVisible(card1), isFalse);
      expect(filtered.cardVisible(card2), isTrue);
    });

    test('clearing the filters shows everything without asking', () async {
      queried.matchingIds = {'card-2'};
      filtered.setSearchQuery('two');
      await pumpEventQueue();
      final asked = queried.queries.length;

      filtered.clearSearchQuery();
      await pumpEventQueue();

      expect(queried.queries, hasLength(asked));
      expect(filtered.cardVisible(filtered.swimlanes[0].cards.single), isTrue);
    });

    test(
      "the Hierarchy keeps a match's ancestors and drops the rest",
      () async {
        queried.matchingIds = {'child-1'};
        filtered.setSearchQuery('child');
        await pumpEventQueue();

        final root = filtered.hierarchyRoots.single;
        expect(root.item.id, 'root-1');
        expect(root.children.single.item.id, 'child-1');
      },
    );

    test('a hidden status still hides a matched item', () async {
      queried.matchingIds = {'root-1', 'root-2'};
      filtered
        ..setSearchQuery('root')
        ..toggleStatusVisibility('done');
      await pumpEventQueue();

      expect(filtered.hierarchyRoots.map((n) => n.item.id), ['root-1']);
    });

    test("a late response can't overwrite a newer one", () async {
      final pending = queried.pendingQueries = [];
      filtered.setSearchQuery('old');
      await pumpEventQueue();
      filtered.setSearchQuery('new');
      await pumpEventQueue();

      pending[1].complete([queried.server['card-2']!]);
      await pumpEventQueue();
      pending[0].complete([queried.server['card-1']!]);
      await pumpEventQueue();

      expect(filtered.cardVisible(filtered.swimlanes[0].cards.single), isFalse);
      expect(filtered.cardVisible(filtered.swimlanes[1].cards.single), isTrue);
    });

    test('a response for filters since cleared is ignored', () async {
      final pending = queried.pendingQueries = [];
      filtered.setSearchQuery('old');
      await pumpEventQueue();
      filtered.clearSearchQuery();

      pending.single.complete(const []);
      await pumpEventQueue();

      expect(filtered.cardVisible(filtered.swimlanes[0].cards.single), isTrue);
    });

    test('a failed query keeps the board and reports it', () async {
      queried.matchingIds = {'card-2'};
      filtered.setSearchQuery('two');
      await pumpEventQueue();
      queried.queryError = const ApiException('down');

      filtered.setSearchQuery('three');
      await pumpEventQueue();

      expect(filtered.moveError, isNotNull);
      expect(filtered.cardVisible(filtered.swimlanes[1].cards.single), isTrue);
    });

    test('an edit while filtering asks again, since it may change what '
        'matches', () async {
      queried.matchingIds = {'card-2'};
      filtered.setSearchQuery('two');
      await pumpEventQueue();
      final asked = queried.queries.length;

      queried.matchingIds = {'card-1', 'card-2'};
      await filtered.setTags('card-1', ['two']);
      await pumpEventQueue();

      expect(queried.queries.length, greaterThan(asked));
      expect(filtered.cardVisible(filtered.swimlanes[0].cards.single), isTrue);
    });
  });

  group('done hiding', () {
    late _TestBoardRepository queried;
    late BoardViewModel board;

    setUp(() async {
      queried = _TestBoardRepository()..matchingIds = {'lane-a', 'lane-b'};
      board = BoardViewModel(queried, WorkItemStore(), FakeViewRepository());
      await board.load();
      await pumpEventQueue();
    });

    tearDown(() => board.dispose());

    WorkItemCard card(int lane) => board.swimlanes[lane].cards.single;

    test('is on by default, asking the server to hide old Done items', () {
      expect(board.showDone, isFalse);
      expect(queried.queries.single.applyDoneHiding, isTrue);
      expect(queried.queries.single.spaceIds, {'home'});
    });

    test('hides the items the server leaves out', () {
      expect(board.cardVisible(card(0)), isFalse);
      expect(board.cardVisible(card(1)), isFalse);
    });

    test('Show done shows every item without asking', () async {
      final asked = queried.queries.length;

      board.setShowDone(true);
      await pumpEventQueue();

      expect(board.showDone, isTrue);
      expect(queried.queries, hasLength(asked));
      expect(board.cardVisible(card(0)), isTrue);
      expect(board.cardVisible(card(1)), isTrue);
    });

    test('turning Show done off again asks again', () async {
      board.setShowDone(true);
      await pumpEventQueue();
      queried.matchingIds = {'lane-a', 'lane-b', 'card-2'};

      board.setShowDone(false);
      await pumpEventQueue();

      expect(queried.queries.last.applyDoneHiding, isTrue);
      expect(board.cardVisible(card(0)), isFalse);
      expect(board.cardVisible(card(1)), isTrue);
    });

    test('Show done keeps the other filters but drops done hiding', () async {
      board
        ..setShowDone(true)
        ..setSearchQuery('card');
      await pumpEventQueue();

      expect(queried.queries.last.text, 'card');
      expect(queried.queries.last.applyDoneHiding, isFalse);
    });

    test('an edit that changes nothing the server matches redraws only its '
        'lane', () async {
      var boardRedraws = 0;
      board.addListener(() => boardRedraws++);

      await board.assign('card-1', 'u1');
      await pumpEventQueue();

      expect(queried.queries.length, greaterThan(1));
      expect(boardRedraws, 0);
    });
  });

  test('derived values are reused until something changes', () {
    final roots = viewModel.hierarchyRoots;
    final statuses = viewModel.visibleStatuses;

    expect(viewModel.hierarchyRoots, same(roots));
    expect(viewModel.visibleStatuses, same(statuses));

    viewModel.toggleStatusVisibility('done');

    expect(viewModel.visibleStatuses, isNot(same(statuses)));
    expect(viewModel.visibleStatuses.map((s) => s.id), ['todo']);
  });

  test('hiddenStatusIds is read-only', () {
    viewModel.toggleStatusVisibility('done');

    expect(() => viewModel.hiddenStatusIds.add('todo'), throwsUnsupportedError);
  });

  group('refreshAfterAdminChanges', () {
    test(
      'reloads the users, the current scope and a loaded hierarchy',
      () async {
        final repository = _TestBoardRepository(
          users: const [
            User(id: 'u1', username: 'alice', kind: UserKind.human),
          ],
        );
        final viewModel = BoardViewModel(
          repository,
          WorkItemStore(),
          FakeViewRepository(),
        );
        addTearDown(viewModel.dispose);
        await viewModel.load();
        await viewModel.loadHierarchy();
        repository.users = const [
          User(id: 'u1', username: 'alicia', kind: UserKind.human),
        ];
        final scopeLoadsBefore = repository.requestedScopes.length;
        final hierarchyLoadsBefore = repository.loadAllItemsCallCount;

        await viewModel.refreshAfterAdminChanges();

        expect(viewModel.usernameFor('u1'), 'alicia');
        expect(repository.requestedScopes.length, scopeLoadsBefore + 1);
        expect(repository.loadAllItemsCallCount, hierarchyLoadsBefore + 1);
      },
    );

    test('leaves an unopened hierarchy unloaded', () async {
      final repository = _TestBoardRepository();
      final viewModel = BoardViewModel(
        repository,
        WorkItemStore(),
        FakeViewRepository(),
      );
      addTearDown(viewModel.dispose);
      await viewModel.load();

      await viewModel.refreshAfterAdminChanges();

      expect(repository.loadAllItemsCallCount, 0);
    });
  });

  group('the shared item store', () {
    WorkItemCard card1() => viewModel.swimlanes[0].cards.single;

    test('every board write sends the version the last one returned', () async {
      await viewModel.moveCard(card1(), 'done');
      await viewModel.setTags('card-1', ['urgent']);
      await viewModel.assign('card-1', 'user-1');
      await viewModel.rescheduleCard(card1(), DateTime(2026, 3, 1), null);
      await viewModel.reparentCard(card1(), 'lane-b');

      expect(repository.sentVersions, [
        'card-1@1',
        'card-1@2',
        'card-1@3',
        'card-1@4',
        'card-1@5',
      ]);
      expect(items['card-1']?.version, 6);
      expect(viewModel.moveError, isNull);
    });

    test('a write started while another on the same card is in flight waits '
        'for it and sends the newer version', () async {
      repository.statusGates['card-1'] = Completer<void>();
      final move = viewModel.moveCard(card1(), 'done');
      final tag = viewModel.setTags('card-1', ['urgent']);
      await pumpEventQueue();

      expect(repository.tagUpdates, isEmpty, reason: 'still queued');
      expect(card1().statusId, 'done');
      expect(card1().tags, ['urgent']);

      repository.statusGates['card-1']!.complete();
      await move;
      await tag;

      expect(repository.sentVersions, ['card-1@1', 'card-1@2']);
      expect(card1().statusId, 'done');
      expect(card1().tags, ['urgent']);
      expect(viewModel.moveError, isNull);
    });

    test(
      'a conflict reloads the card, says so, and leaves other cards alone',
      () async {
        repository.editElsewhere(
          'card-1',
          (item) => workItem(
            'card-1',
            number: 1,
            title: 'Renamed elsewhere',
            parentId: 'lane-a',
          ),
        );
        repository.statusGates['card-2'] = Completer<void>();
        final otherMove = viewModel.moveCard(
          viewModel.swimlanes[1].cards.single,
          'done',
        );

        await viewModel.moveCard(card1(), 'done');

        expect(card1().title, 'Renamed elsewhere');
        expect(card1().statusId, 'todo');
        expect(items['card-1']?.version, 2);
        expect(repository.itemLoads, ['card-1']);
        expect(viewModel.moveError, contains('Someone else changed'));
        expect(viewModel.moveError, contains('latest version'));
        expect(
          viewModel.swimlanes[1].cards.single.statusId,
          'done',
          reason: "card-2's move is still in flight and still shown",
        );

        repository.statusGates['card-2']!.complete();
        await otherMove;
        expect(viewModel.swimlanes[1].cards.single.statusId, 'done');
      },
    );

    test('after a conflict the next write uses the reloaded version', () async {
      repository.editElsewhere('card-1', (item) => item);
      await viewModel.moveCard(card1(), 'done');
      viewModel.clearMoveError();

      await viewModel.moveCard(card1(), 'done');

      expect(repository.sentVersions, ['card-1@1', 'card-1@2']);
      expect(card1().statusId, 'done');
      expect(viewModel.moveError, isNull);
    });

    test(
      'a conflict on a card someone else moved shows it in its new lane',
      () async {
        repository.editElsewhere(
          'card-1',
          (item) => item.movedToParent('lane-b'),
        );

        await viewModel.setTags('card-1', ['urgent']);

        expect(viewModel.swimlanes[0].cards, isEmpty);
        expect(viewModel.swimlanes[1].cards.map((c) => c.id), [
          'card-2',
          'card-1',
        ]);
        expect(items['card-1']?.tags, isEmpty);
      },
    );

    test(
      'a conflict on a card moved out of this board takes it off the board',
      () async {
        repository.editElsewhere(
          'card-1',
          (item) => item.movedToParent('elsewhere'),
        );

        await viewModel.moveCard(card1(), 'done');

        expect(viewModel.swimlanes[0].cards, isEmpty);
        expect(viewModel.moveError, isNotNull);
      },
    );

    test('an edit saved to the store elsewhere shows on the board and in the '
        'hierarchy without a reload', () async {
      repository = _TestBoardRepository(
        hierarchyItems: [
          workItem('lane-a', title: 'Lane A'),
          workItem('card-1', title: 'Card 1', parentId: 'lane-a'),
        ],
      );
      viewModel = BoardViewModel(repository, items, FakeViewRepository());
      await viewModel.load();
      await viewModel.loadHierarchy();
      final scopeLoads = repository.requestedScopes.length;
      var itemsChanged = false;
      viewModel.itemChanges.addListener(() => itemsChanged = true);

      await items.write(
        'card-1',
        send: (version) async => withVersion(
          workItem(
            'card-1',
            title: 'Renamed in the dialog',
            parentId: 'lane-a',
          ),
          version + 1,
        ),
        reload: (_) => throw StateError('No conflict expected'),
      );

      expect(itemsChanged, isTrue);
      expect(card1().title, 'Renamed in the dialog');
      expect(
        viewModel.hierarchyRoots.single.children.single.item.title,
        'Renamed in the dialog',
      );
      expect(repository.requestedScopes.length, scopeLoads);
    });

    group('narrow notifications', () {
      Future<void> renameCard1() => items.write(
        'card-1',
        send: (version) async => withVersion(
          workItem(
            'card-1',
            title: 'Renamed',
            parentId: 'lane-a',
            statusId: 'todo',
          ),
          version + 1,
        ),
        reload: (_) => throw StateError('No conflict expected'),
      );

      test('a card edit notifies only its own lane', () async {
        final notifiedLanes = <String>[];
        for (final lane in viewModel.swimlaneListenables) {
          lane.addListener(() => notifiedLanes.add(lane.value.parentId));
        }

        await renameCard1();

        expect(notifiedLanes.toSet(), {'lane-a'});
        expect(
          viewModel.swimlaneListenables.first.value.cards.single.title,
          'Renamed',
        );
      });

      test('a card edit does not notify the whole board', () async {
        var boardNotified = false;
        viewModel.addListener(() => boardNotified = true);

        await renameCard1();

        expect(boardNotified, isFalse);
      });

      test('renaming a lane notifies the whole board, whose drill-down '
          'targets show lane titles', () async {
        var boardNotified = false;
        viewModel.addListener(() => boardNotified = true);

        await items.write(
          'lane-a',
          send: (version) async =>
              withVersion(workItem('lane-a', title: 'Lane A2'), version + 1),
          reload: (_) => throw StateError('No conflict expected'),
        );

        expect(boardNotified, isTrue);
        expect(viewModel.drillDownTargets.first.title, 'Lane A2');
      });

      test('a lane leaving the board notifies the whole board', () {
        var boardNotified = false;
        viewModel.addListener(() => boardNotified = true);

        items.remove('lane-a');

        expect(boardNotified, isTrue);
        expect(
          viewModel.swimlaneListenables.map((lane) => lane.value.parentId),
          ['lane-b'],
        );
      });

      test('a lane listenable carries its lane through a card move', () async {
        final laneA = viewModel.swimlaneListenables.first;
        var laneANotified = 0;
        laneA.addListener(() => laneANotified++);

        await viewModel.moveCard(card1(), 'done');

        expect(laneANotified, greaterThan(0));
        expect(laneA.value.cards.single.statusId, 'done');
      });
    });

    test('an item removed from the store leaves the board', () {
      items.remove('card-1');

      expect(viewModel.swimlanes[0].cards, isEmpty);
      expect(viewModel.swimlanes[1].cards.single.id, 'card-2');
    });

    test('a board disposed of stops listening to the store', () {
      viewModel.dispose();

      expect(() => items.remove('card-1'), returnsNormally);
    });
  });

  group('the Roadmap asks the server for its own window', () {
    final november = (
      start: DateTime(2026, 11, 1),
      end: DateTime(2026, 11, 30),
    );
    late _TestBoardRepository queried;
    late BoardViewModel roadmap;

    setUp(() async {
      queried = _TestBoardRepository(
        hierarchyItems: [
          workItem('root-1', number: 1, parentId: null, statusId: 'todo'),
          workItem('child-1', number: 2, parentId: 'root-1', statusId: 'todo'),
          workItem('root-2', number: 3, parentId: null, statusId: 'todo'),
        ],
      );
      roadmap = BoardViewModel(queried, WorkItemStore(), FakeViewRepository());
      await roadmap.load();
      await roadmap.loadHierarchy();
      await pumpEventQueue();
      queried.queries.clear();
    });

    test('showing a window asks for it with the header filters', () async {
      roadmap
        ..setSearchQuery('paint')
        ..showRoadmapWindow(november);
      await pumpEventQueue();

      final query = queried.queries.last;
      expect(query.text, 'paint');
      expect(query.from, DateTime(2026, 11, 1));
      expect(query.to, DateTime(2026, 11, 30));
    });

    test(
      'its rows are what the server matched, under their ancestors',
      () async {
        queried.matchingIds = {'child-1'};
        roadmap.showRoadmapWindow(november);
        await pumpEventQueue();

        final root = roadmap.roadmapRoots.single;
        expect(root.item.id, 'root-1');
        expect(root.children.single.item.id, 'child-1');
      },
    );

    test('the Hierarchy is not narrowed by the Roadmap window', () async {
      queried.matchingIds = {'child-1'};
      roadmap.showRoadmapWindow(november);
      await pumpEventQueue();

      expect(roadmap.hierarchyRoots.map((n) => n.item.id), [
        'root-1',
        'root-2',
      ]);
    });

    test('it shows no rows until the first answer arrives', () async {
      final pending = queried.pendingQueries = [];
      roadmap.showRoadmapWindow(november);

      expect(roadmap.isRoadmapMatching, isTrue);
      expect(roadmap.roadmapRoots, isEmpty);

      pending.single.complete([queried.server['root-2']!]);
      await pumpEventQueue();

      expect(roadmap.isRoadmapMatching, isFalse);
      expect(roadmap.roadmapRoots.map((n) => n.item.id), ['root-2']);
    });

    test('a failed first answer is an error the Roadmap can retry', () async {
      queried.queryError = const ApiException('down');
      roadmap.showRoadmapWindow(november);
      await pumpEventQueue();

      expect(roadmap.isRoadmapMatching, isFalse);
      expect(roadmap.roadmapLoadError, isNotNull);

      queried
        ..queryError = null
        ..matchingIds = {'root-2'};
      await roadmap.retryRoadmap();

      expect(roadmap.roadmapLoadError, isNull);
      expect(roadmap.roadmapRoots.map((n) => n.item.id), ['root-2']);
    });

    test(
      'a failure after an answer keeps the rows and reports it once',
      () async {
        queried.matchingIds = {'root-2'};
        roadmap.showRoadmapWindow(november);
        await pumpEventQueue();
        queried.queryError = const ApiException('down');

        roadmap.showRoadmapWindow((
          start: DateTime(2026, 12, 1),
          end: DateTime(2026, 12, 31),
        ));
        await pumpEventQueue();

        expect(roadmap.roadmapLoadError, isNull);
        expect(roadmap.moveError, isNotNull);
        expect(roadmap.roadmapRoots.map((n) => n.item.id), ['root-2']);
      },
    );

    test(
      'a header range outside the window shows nothing without asking',
      () async {
        roadmap
          ..setTimeFilter(start: DateTime(2027, 1, 1))
          ..showRoadmapWindow(november);
        await pumpEventQueue();

        expect(
          queried.queries.where((q) => q.from == DateTime(2026, 11, 1)),
          isEmpty,
        );
        expect(roadmap.isRoadmapMatching, isFalse);
        expect(roadmap.roadmapRoots, isEmpty);
      },
    );

    test('a header filter change asks for the window again', () async {
      roadmap.showRoadmapWindow(november);
      await pumpEventQueue();
      queried.queries.clear();

      roadmap.toggleTagFilter('urgent');
      await pumpEventQueue();

      expect(
        queried.queries
            .where((q) => q.from == DateTime(2026, 11, 1))
            .single
            .tags,
        {'urgent'},
      );
    });

    test('an edit asks again while the Roadmap is open, and not after it '
        'closes', () async {
      roadmap.showRoadmapWindow(november);
      await pumpEventQueue();
      queried.queries.clear();

      await roadmap.setTags('root-1', ['x']);
      await pumpEventQueue();
      final whileOpen = queried.queries
          .where((q) => q.from == DateTime(2026, 11, 1))
          .length;

      roadmap.closeRoadmap();
      queried.queries.clear();
      await roadmap.setTags('root-1', ['y']);
      await pumpEventQueue();

      expect(whileOpen, greaterThan(0));
      expect(
        queried.queries.where((q) => q.from == DateTime(2026, 11, 1)),
        isEmpty,
      );
    });
  });
}
