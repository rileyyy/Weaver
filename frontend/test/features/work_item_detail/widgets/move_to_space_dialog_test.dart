import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/data/space_move_repository.dart';
import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/features/work_item_detail/models/space_move_mapping.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/space_move_view_model.dart';
import 'package:weaver/features/work_item_detail/widgets/move_to_space_dialog.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

import '../../../shared/fakes/work_items.dart';

class _FakeRepository implements SpaceMoveRepository {
  SpaceMoveChoices? movedWith;

  @override
  Future<List<Space>> loadSpaces() async => const [
    Space(id: 'home', name: 'Home'),
    Space(id: 'workshop', name: 'Workshop'),
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
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) async =>
      const [];

  @override
  Future<WorkItem> getItem(String id) async => workItem(id);

  @override
  Future<SpaceMovePlan> planMove(String id, String targetSpaceId) async =>
      const SpaceMovePlan(
        itemCount: 3,
        trashedItemCount: 1,
        clearedFieldValueCount: 1,
        statuses: [
          SpaceMoveMapping(
            sourceId: 'todo',
            sourceName: 'To Do',
            targetId: 'quoted',
            itemCount: 2,
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

  @override
  Future<WorkItem> moveToSpace(
    String id,
    SpaceMoveChoices choices, {
    required int expectedVersion,
  }) async {
    movedWith = choices;
    return workItem(id, spaceId: choices.targetSpaceId, version: 2);
  }
}

void main() {
  testWidgets('shows the mapping for the picked Space, then moves', (
    tester,
  ) async {
    final repository = _FakeRepository();
    final item = workItem('shed', spaceId: 'home');
    final store = WorkItemStore()..putAll([item], asOf: 0);
    bool? result;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () async => result = await showMoveToSpaceDialog(
              context,
              item: item,
              viewModel: SpaceMoveViewModel(repository, store),
            ),
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    expect(find.text('Home'), findsNothing, reason: "the item's own Space");
    await tester.tap(find.byKey(const ValueKey('move-target-space')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Workshop').last);
    await tester.pumpAndSettle();

    expect(
      find.textContaining('2 sub-items (1 of them in the Trash) move'),
      findsOneWidget,
    );
    expect(
      find.textContaining('1 custom field value is cleared'),
      findsOneWidget,
    );
    expect(find.text('To Do (2)'), findsOneWidget);
    expect(find.text('Quoted'), findsOneWidget);
    expect(find.text('No Type'), findsOneWidget);

    await tester.tap(find.byKey(const ValueKey('move-map-todo')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Booked').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Move'));
    await tester.pumpAndSettle();

    expect(repository.movedWith!.statuses, {'todo': 'booked'});
    expect(repository.movedWith!.types, {'room': null});
    expect(result, isTrue);
  });
}
