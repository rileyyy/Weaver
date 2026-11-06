import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/space_move_repository.dart';
import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/features/work_item_detail/models/space_move_mapping.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/space_move_view_model.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

import '../../shared/fakes/work_items.dart';

const _plan = SpaceMovePlan(
  itemCount: 3,
  clearedFieldValueCount: 1,
  statuses: [
    SpaceMoveMapping(
      sourceId: 'todo',
      sourceName: 'To Do',
      targetId: 'quoted',
      itemCount: 2,
    ),
    SpaceMoveMapping(
      sourceId: 'doing',
      sourceName: 'Doing',
      targetId: 'booked',
      itemCount: 1,
    ),
  ],
  types: [
    SpaceMoveMapping(
      sourceId: 'room',
      sourceName: 'Room',
      targetId: null,
      itemCount: 1,
    ),
  ],
  priorities: [],
);

class _FakeRepository implements SpaceMoveRepository {
  final List<(String, SpaceMoveChoices, int)> moves = [];
  final List<String> plannedSpaces = [];
  ApiException? moveError;
  WorkItem? movedItem;

  @override
  Future<List<Space>> loadSpaces() async => const [
    Space(id: 'home', name: 'Home'),
    Space(id: 'workshop', name: 'Workshop'),
    Space(id: 'village', name: 'Village'),
  ];

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) async => const [
    WorkItemStatus(id: 'quoted', name: 'Quoted', order: 0),
    WorkItemStatus(id: 'booked', name: 'Booked', order: 1),
  ];

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) async => const [
    WorkItemType(id: 'chore', name: 'Chore', order: 0),
  ];

  @override
  Future<List<WorkItemPriority>> loadPriorities(String spaceId) async =>
      const [];

  @override
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) async => [
    workItem('bench', spaceId: spaceId),
  ];

  @override
  Future<WorkItem> getItem(String id) async => workItem(id);

  @override
  Future<SpaceMovePlan> planMove(String id, String targetSpaceId) async {
    plannedSpaces.add(targetSpaceId);
    return _plan;
  }

  @override
  Future<WorkItem> moveToSpace(
    String id,
    SpaceMoveChoices choices, {
    required int expectedVersion,
  }) async {
    if (moveError case final error?) throw error;
    moves.add((id, choices, expectedVersion));
    return movedItem ??
        workItem(id, spaceId: choices.targetSpaceId, version: 2);
  }
}

void main() {
  late _FakeRepository repository;
  late WorkItemStore store;
  late SpaceMoveViewModel viewModel;
  final item = workItem('shed', spaceId: 'home', version: 1);

  setUp(() async {
    repository = _FakeRepository();
    store = WorkItemStore()..putAll([item], asOf: 0);
    viewModel = SpaceMoveViewModel(repository, store);
    await viewModel.start(item);
  });

  test("offers every Space but the item's own", () {
    expect(viewModel.spaces.map((s) => s.id), ['workshop', 'village']);
    expect(viewModel.canMove, isFalse);
  });

  test('picking a Space shows the plan with its defaults chosen', () async {
    await viewModel.selectSpace('workshop');

    expect(repository.plannedSpaces, ['workshop']);
    expect(viewModel.plan, same(_plan));
    expect(viewModel.statusChoice('todo'), 'quoted');
    expect(viewModel.typeChoice('room'), isNull);
    expect(viewModel.targetStatuses.map((s) => s.name), ['Quoted', 'Booked']);
    expect(viewModel.canMove, isTrue);
  });

  test('moves with the defaults and any changed choice', () async {
    await viewModel.selectSpace('workshop');
    viewModel
      ..chooseStatus('doing', 'quoted')
      ..chooseType('room', 'chore')
      ..chooseParent(workItem('bench', spaceId: 'workshop'));

    final moved = await viewModel.move();

    expect(moved, isTrue);
    final (id, choices, version) = repository.moves.single;
    expect(id, 'shed');
    expect(version, 1);
    expect(choices.targetSpaceId, 'workshop');
    expect(choices.parentId, 'bench');
    expect(choices.statuses, {'todo': 'quoted', 'doing': 'quoted'});
    expect(choices.types, {'room': 'chore'});
    expect(choices.priorities, isEmpty);
    expect(store['shed']!.spaceId, 'workshop');
  });

  test('picking another Space resets the parent and choices', () async {
    await viewModel.selectSpace('workshop');
    viewModel
      ..chooseStatus('doing', 'quoted')
      ..chooseParent(workItem('bench', spaceId: 'workshop'));

    await viewModel.selectSpace('village');

    expect(viewModel.parent, isNull);
    expect(viewModel.statusChoice('doing'), 'booked');
  });

  test('parents are searched in the target Space', () async {
    await viewModel.selectSpace('village');

    final found = await viewModel.searchParents('ben');

    expect(found.single.spaceId, 'village');
  });

  test(
    "a refused move shows the server's reason and keeps the dialog",
    () async {
      await viewModel.selectSpace('workshop');
      repository.moveError = const ApiException(
        "Status x isn't one of the target Space's; map to those.",
        statusCode: 400,
      );

      final moved = await viewModel.move();

      expect(moved, isFalse);
      expect(viewModel.error, contains('map to those'));
      expect(viewModel.isMoving, isFalse);
      expect(store['shed']!.spaceId, 'home');
    },
  );
}
