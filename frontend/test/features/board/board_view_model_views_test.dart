import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';
import 'package:weaver/shared/models/work_item_status.dart';

import '../../shared/fakes/work_items.dart';
import 'fakes/fake_view_repository.dart';

const _home = Space(id: 'home', name: 'Home');
const _village = Space(id: 'village', name: 'Village');

const _overview = SavedView(
  id: overviewId,
  builtIn: true,
  definition: ViewDefinition(name: 'Overview', scheduledOrInProgressOnly: true),
);
const _homeOnly = SavedView(
  id: 'home-only',
  definition: ViewDefinition(name: 'Home only', spaceIds: {'home'}),
);
const _villageOnly = SavedView(
  id: 'village-only',
  definition: ViewDefinition(
    name: 'Village only',
    spaceIds: {'village'},
    text: 'fence',
    tags: {'outside'},
    tagMatch: TagMatch.any,
  ),
);

/// Each Space's top level is one lane named after it, with one card, so a
/// test can tell which Spaces the board is showing.
class _SpacesRepository implements BoardRepository {
  List<Space> spaces = const [_home, _village];
  final List<String> boardLoads = [];
  final List<String> allItemLoads = [];
  final List<String> creates = [];
  final List<String> reparents = [];
  final List<WorkItemQuery> queries = [];

  /// Queries for exactly these Space ids wait on their completer.
  final Map<String, Completer<void>> queryGates = {};

  /// Item loads for these Space ids wait on their completer.
  final Map<String, Completer<void>> allItemGates = {};

  static String _key(WorkItemQuery query) =>
      (query.spaceIds.toList()..sort()).join('+');

  @override
  Future<List<Space>> loadSpaces() async => spaces;

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async {
    boardLoads.add('$spaceId/$scopeItemId');
    return BoardData(
      statuses: [WorkItemStatus(id: '$spaceId-todo', name: 'To Do', order: 0)],
      swimlanes: [
        SwimlaneItems(
          lane: workItem('$spaceId-lane', spaceId: spaceId),
          cards: [
            workItem(
              '$spaceId-card',
              spaceId: spaceId,
              parentId: '$spaceId-lane',
              statusId: '$spaceId-todo',
            ),
          ],
        ),
      ],
    );
  }

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) async {
    allItemLoads.add(spaceId);
    await allItemGates[spaceId]?.future;
    return [workItem('$spaceId-item', spaceId: spaceId)];
  }

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) async => const [];

  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async {
    queries.add(query);
    await queryGates[_key(query)]?.future;
    // Every item of the asked Spaces matches.
    return [
      for (final spaceId in query.spaceIds) ...[
        workItem('$spaceId-card', spaceId: spaceId),
        workItem('$spaceId-item', spaceId: spaceId),
      ],
    ];
  }

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String spaceId,
    required String? parentId,
    required String statusId,
  }) async => creates.add('$title in $spaceId under $parentId');

  @override
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
    required int expectedVersion,
  }) async {
    reparents.add('$itemId under $newParentId');
    return workItem(itemId, parentId: newParentId);
  }

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  Future<WorkItem> getItem(String id) => throw UnimplementedError();

  @override
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  }) => throw UnimplementedError();

  @override
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) => throw UnimplementedError();

  @override
  Future<WorkItem> assign(
    String workItemId,
    String? userId, {
    required int expectedVersion,
  }) => throw UnimplementedError();

  @override
  Future<WorkItem> setTags(
    String workItemId,
    List<String> tags, {
    required int expectedVersion,
  }) => throw UnimplementedError();
}

void main() {
  late _SpacesRepository repository;
  late FakeViewRepository views;

  Future<BoardViewModel> loaded({SavedView defaultView = _overview}) async {
    views = FakeViewRepository(defaultView: defaultView);
    final viewModel = BoardViewModel(repository, WorkItemStore(), views);
    await viewModel.load();
    return viewModel;
  }

  setUp(() => repository = _SpacesRepository());

  group('on launch', () {
    test("opens the user's default View", () async {
      final viewModel = await loaded(defaultView: _villageOnly);

      expect(viewModel.currentView?.id, 'village-only');
      expect(viewModel.currentSpace, _village);
      expect(repository.boardLoads, ['village/null']);
      expect(viewModel.searchQuery, 'fence');
    });

    test('a View of one Space starts at that Space', () async {
      final viewModel = await loaded(defaultView: _homeOnly);

      expect(viewModel.breadcrumbs.single.title, 'Home');
      expect(viewModel.breadcrumbs.single.spaceId, 'home');
    });

    test('Overview shows a section per Space, each with its own statuses, '
        'under one root crumb', () async {
      final viewModel = await loaded();

      expect(viewModel.spansSpaces, isTrue);
      expect(viewModel.currentSpace, isNull);
      expect(viewModel.breadcrumbs.single.title, 'Overview');
      expect(
        [
          for (final section in viewModel.boardSections)
            [
              section.space.name,
              ...section.statuses.map((s) => s.id),
              ...section.lanes.map((l) => l.value.parentId),
            ].join(' '),
        ],
        ['Home home-todo home-lane', 'Village village-todo village-lane'],
      );
    });

    test("asks for Overview's rule over every Space shown", () async {
      await loaded();
      await pumpEventQueue();

      final query = repository.queries.last;
      expect(query.scheduledOrInProgressOnly, isTrue);
      expect(query.spaceIds, {'home', 'village'});
    });
  });

  test('with no Spaces there is no board to show or create in', () async {
    repository.spaces = const [];
    final viewModel = await loaded();

    expect(viewModel.hasNoSpaces, isTrue);
    expect(viewModel.boardSections, isEmpty);
    expect(viewModel.loadError, isNull);
    expect(viewModel.canCreateWorkItem, isFalse);
    expect(repository.boardLoads, isEmpty);
  });

  group('opening a View', () {
    test(
      "shows its Spaces at their top level, with its query's inputs",
      () async {
        final viewModel = await loaded(defaultView: _homeOnly);
        await viewModel.drillIntoScope(
          const ScopeCrumb(id: 'home-lane', title: 'home-lane'),
        );

        await viewModel.openView(_villageOnly);

        expect(viewModel.currentView?.id, 'village-only');
        expect(repository.boardLoads.last, 'village/null');
        expect(viewModel.breadcrumbs.map((c) => c.title), ['Village']);
        expect(viewModel.searchQuery, 'fence');
        expect(viewModel.isTagFilterSelected('outside'), isTrue);
      },
    );

    test('keeps the time range, which a View does not store', () async {
      final viewModel = await loaded(defaultView: _homeOnly);
      viewModel.setTimeFilter(start: DateTime(2026, 11, 1));

      await viewModel.openView(_villageOnly);

      expect(viewModel.filterStart, DateTime(2026, 11, 1));
    });

    test('asks the new Spaces which items match', () async {
      final viewModel = await loaded(defaultView: _homeOnly);
      await pumpEventQueue();

      await viewModel.openView(_villageOnly);
      await pumpEventQueue();

      expect(repository.queries.last.spaceIds, {'village'});
      expect(repository.queries.last.text, 'fence');
    });

    test(
      "a late answer for the old View can't hide the new one's items",
      () async {
        final viewModel = await loaded(defaultView: _homeOnly);
        final homeAnswer = repository.queryGates['home'] = Completer();
        viewModel.setSearchQuery('paint');
        await pumpEventQueue();

        await viewModel.openView(_villageOnly);
        homeAnswer.complete();
        await pumpEventQueue();

        final villageCard = viewModel.swimlanes.single.cards.single;
        expect(viewModel.cardVisible(villageCard), isTrue);
      },
    );

    test('reloads the hierarchy for its Spaces once it was loaded', () async {
      final viewModel = await loaded(defaultView: _homeOnly);
      await viewModel.loadHierarchy();

      await viewModel.openView(_villageOnly);

      expect(repository.allItemLoads, ['home', 'village']);
      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
    });

    test('never shows the old Space in the hierarchy while loading', () async {
      final viewModel = await loaded(defaultView: _homeOnly);
      await viewModel.loadHierarchy();
      final villageItems = repository.allItemGates['village'] = Completer();

      final opening = viewModel.openView(_villageOnly);
      await pumpEventQueue();

      expect(viewModel.hierarchyRoots, isEmpty);
      villageItems.complete();
      await opening;
      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
    });

    test(
      'a hierarchy load for the old View still in flight is dropped',
      () async {
        final viewModel = await loaded(defaultView: _homeOnly);
        final homeItems = repository.allItemGates['home'] = Completer();
        final firstLoad = viewModel.loadHierarchy();

        await viewModel.openView(_villageOnly);
        homeItems.complete();
        await firstLoad;

        expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
        expect(viewModel.isHierarchyLoading, isFalse);
      },
    );
  });

  group('the breadcrumb across Spaces', () {
    test('drilling in from the View pushes the Space, then the item', () async {
      final viewModel = await loaded();

      await viewModel.drillIntoScope(viewModel.drillDownTargets.first);

      expect(viewModel.breadcrumbs.map((c) => c.title), [
        'Overview',
        'Home',
        'home-lane',
      ]);
      expect(viewModel.currentSpace, _home);
      expect(repository.boardLoads.last, 'home/home-lane');
    });

    test("focusing a Space shows only that Space's top level", () async {
      final viewModel = await loaded();

      await viewModel.focusSpace(_village);
      await pumpEventQueue();

      expect(viewModel.breadcrumbs.map((c) => c.title), [
        'Overview',
        'Village',
      ]);
      expect(viewModel.boardSections.single.space, _village);
      expect(repository.queries.last.spaceIds, {'village'});
    });

    test('going back to the View shows every Space again', () async {
      final viewModel = await loaded();
      await viewModel.focusSpace(_village);

      await viewModel.navigateToBreadcrumb(0);

      expect(viewModel.boardSections.map((s) => s.space), [_home, _village]);
    });

    test('the Hierarchy follows the Space the breadcrumb is in', () async {
      final viewModel = await loaded();
      await viewModel.loadHierarchy();

      await viewModel.focusSpace(_village);

      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
    });
  });

  test('the Hierarchy groups its trees by Space while several show', () async {
    final viewModel = await loaded();
    await viewModel.loadHierarchy();

    expect(
      [
        for (final section in viewModel.hierarchySections)
          (section.space.name, section.roots.single.item.id),
      ],
      [('Home', 'home-item'), ('Village', 'village-item')],
    );
  });

  group('creating', () {
    test('needs one Space to create in', () async {
      final viewModel = await loaded();

      expect(viewModel.canCreateWorkItem, isFalse);
      await viewModel.focusSpace(_home);
      expect(viewModel.canCreateWorkItem, isTrue);
    });

    test('a top-level item is created in the Space shown', () async {
      final viewModel = await loaded(defaultView: _villageOnly);

      await viewModel.createWorkItem(
        title: 'Fix the fence',
        parentId: null,
        statusId: 'todo',
      );

      expect(repository.creates, ['Fix the fence in village under null']);
    });
  });

  test("a card can't be moved to another Space's lane", () async {
    final viewModel = await loaded();
    final homeCard = viewModel.swimlanes.first.cards.single;

    await viewModel.reparentCard(homeCard, 'village-lane');

    expect(repository.reparents, isEmpty);
  });

  test('showing other Spaces keeps the rest of the filters', () async {
    final viewModel = await loaded(defaultView: _homeOnly);
    viewModel.setSearchQuery('paint');

    await viewModel.setSpaces({'home', 'village'});

    expect(viewModel.boardSections.map((s) => s.space), [_home, _village]);
    expect(viewModel.searchQuery, 'paint');
    expect(viewModel.breadcrumbs.single.title, 'Home only');
  });

  group('saving', () {
    test(
      'the header state is saved as a View with a name and layout',
      () async {
        final viewModel = await loaded(defaultView: _villageOnly);
        viewModel.setShowDone(true);

        final definition = viewModel.definitionFor(
          'Fences',
          ViewLayout.roadmap,
        );

        expect(definition.name, 'Fences');
        expect(definition.spaceIds, {'village'});
        expect(definition.text, 'fence');
        expect(definition.applyDoneHiding, isFalse);
        expect(definition.layout, ViewLayout.roadmap);
      },
    );

    test('adopting the saved View names the root crumb after it', () async {
      final viewModel = await loaded();
      const saved = SavedView(
        id: 'saved',
        definition: ViewDefinition(name: 'Everything now'),
      );

      viewModel.adoptView(saved);

      expect(viewModel.currentView?.id, 'saved');
      expect(viewModel.breadcrumbs.single.title, 'Everything now');
    });
  });

  group('after admin changes', () {
    test('a renamed Space shows its new name', () async {
      final viewModel = await loaded(defaultView: _homeOnly);
      repository.spaces = const [Space(id: 'home', name: 'House'), _village];

      await viewModel.refreshAfterAdminChanges();

      expect(viewModel.currentSpace?.name, 'House');
      expect(viewModel.breadcrumbs.first.title, 'House');
      expect(viewModel.spaces.first.name, 'House');
    });

    test('the first Space opens once an admin has created one', () async {
      repository.spaces = const [];
      final viewModel = await loaded();
      repository.spaces = const [_home];

      await viewModel.refreshAfterAdminChanges();

      expect(viewModel.hasNoSpaces, isFalse);
      expect(viewModel.currentSpace, _home);
      expect(viewModel.swimlanes.single.parentId, 'home-lane');
    });
  });
}
