import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';

import '../../shared/fakes/memory_last_space_store.dart';
import '../../shared/fakes/work_items.dart';

const _home = Space(id: 'home', name: 'Home');
const _village = Space(id: 'village', name: 'Village');

/// Each Space's top level is one lane named after it, so a test can tell
/// which Space the board is showing.
class _SpacesRepository implements BoardRepository {
  List<Space> spaces = const [_home, _village];
  final List<String> boardLoads = [];
  final List<String> allItemLoads = [];
  final List<String> creates = [];

  /// Item loads for these Space ids wait on their completer.
  final Map<String, Completer<void>> allItemGates = {};

  @override
  Future<List<Space>> loadSpaces() async => spaces;

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async {
    boardLoads.add('$spaceId/$scopeItemId');
    return BoardData(
      statuses: const [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
      swimlanes: [
        SwimlaneItems(
          lane: workItem('$spaceId-lane', spaceId: spaceId),
          cards: const [],
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
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String spaceId,
    required String? parentId,
    required String statusId,
  }) async => creates.add('$title in $spaceId under $parentId');

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
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
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
  late MemoryLastSpaceStore lastSpace;

  BoardViewModel viewModelWith({String? remembered}) {
    lastSpace = MemoryLastSpaceStore(remembered);
    return BoardViewModel(repository, WorkItemStore(), lastSpace);
  }

  setUp(() => repository = _SpacesRepository());

  group('which Space opens', () {
    test('the last-used Space, when it still exists', () async {
      final viewModel = viewModelWith(remembered: 'village');

      await viewModel.load();

      expect(viewModel.currentSpace, _village);
      expect(repository.boardLoads, ['village/null']);
      expect(viewModel.swimlanes.single.parentId, 'village-lane');
    });

    test('the first Space when none is remembered', () async {
      final viewModel = viewModelWith();

      await viewModel.load();

      expect(viewModel.currentSpace, _home);
    });

    test('the first Space when the remembered one is gone', () async {
      final viewModel = viewModelWith(remembered: 'deleted-space');

      await viewModel.load();

      expect(viewModel.currentSpace, _home);
    });

    test('the root breadcrumb is the Space', () async {
      final viewModel = viewModelWith();

      await viewModel.load();

      expect(viewModel.breadcrumbs.single.id, isNull);
      expect(viewModel.breadcrumbs.single.title, 'Home');
    });
  });

  test('with no Spaces there is no board to show or create in', () async {
    repository.spaces = const [];
    final viewModel = viewModelWith();

    await viewModel.load();

    expect(viewModel.hasNoSpaces, isTrue);
    expect(viewModel.currentSpace, isNull);
    expect(viewModel.swimlanes, isEmpty);
    expect(viewModel.loadError, isNull);
    expect(viewModel.canCreateWorkItem, isFalse);
    expect(repository.boardLoads, isEmpty);
  });

  group('switching Space', () {
    test(
      "shows the other Space's top level, resets the trail and remembers it",
      () async {
        final viewModel = viewModelWith();
        await viewModel.load();
        await viewModel.drillIntoScope(
          const ScopeCrumb(id: 'home-lane', title: 'home-lane'),
        );

        await viewModel.switchSpace(_village);

        expect(viewModel.currentSpace, _village);
        expect(repository.boardLoads.last, 'village/null');
        expect(viewModel.swimlanes.single.parentId, 'village-lane');
        expect(viewModel.breadcrumbs.map((c) => c.title), ['Village']);
        expect(lastSpace.spaceId, 'village');
      },
    );

    test('to the current Space does nothing', () async {
      final viewModel = viewModelWith();
      await viewModel.load();

      await viewModel.switchSpace(_home);

      expect(repository.boardLoads, ['home/null']);
    });

    test('reloads the hierarchy for the new Space once it was loaded', () async {
      final viewModel = viewModelWith();
      await viewModel.load();
      await viewModel.loadHierarchy();

      await viewModel.switchSpace(_village);

      expect(repository.allItemLoads, ['home', 'village']);
      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
    });
  });

  group('switching Space never shows the old Space in the hierarchy', () {
    test('while the new Space is loading', () async {
      final viewModel = viewModelWith();
      await viewModel.load();
      await viewModel.loadHierarchy();
      final villageItems = repository.allItemGates['village'] = Completer();

      final switching = viewModel.switchSpace(_village);
      await pumpEventQueue();

      expect(viewModel.hierarchyRoots, isEmpty);
      villageItems.complete();
      await switching;
      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
    });

    test('when the old Space was still loading', () async {
      final viewModel = viewModelWith();
      await viewModel.load();
      final homeItems = repository.allItemGates['home'] = Completer();
      final firstLoad = viewModel.loadHierarchy();

      await viewModel.switchSpace(_village);
      homeItems.complete();
      await firstLoad;

      expect(viewModel.hierarchyRoots.single.item.id, 'village-item');
      expect(viewModel.isHierarchyLoading, isFalse);
    });
  });

  test('a top-level item is created in the current Space', () async {
    final viewModel = viewModelWith(remembered: 'village');
    await viewModel.load();

    await viewModel.createWorkItem(
      title: 'Fix the fence',
      parentId: null,
      statusId: 'todo',
    );

    expect(repository.creates, ['Fix the fence in village under null']);
  });

  group('after admin changes', () {
    test('a renamed Space shows its new name', () async {
      final viewModel = viewModelWith();
      await viewModel.load();
      repository.spaces = const [Space(id: 'home', name: 'House'), _village];

      await viewModel.refreshAfterAdminChanges();

      expect(viewModel.currentSpace?.name, 'House');
      expect(viewModel.breadcrumbs.first.title, 'House');
      expect(viewModel.spaces.first.name, 'House');
    });

    test('the first Space opens once an admin has created one', () async {
      repository.spaces = const [];
      final viewModel = viewModelWith();
      await viewModel.load();
      repository.spaces = const [_home];

      await viewModel.refreshAfterAdminChanges();

      expect(viewModel.hasNoSpaces, isFalse);
      expect(viewModel.currentSpace, _home);
      expect(viewModel.swimlanes.single.parentId, 'home-lane');
    });
  });
}
