import 'package:flutter/material.dart';
import 'package:flutter/widgets.dart' as widgets show debugOnRebuildDirtyWidget;
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/views/swimlane/widgets/status_column.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

import '../../../../shared/fakes/memory_last_space_store.dart';
import '../../../../shared/fakes/work_items.dart';

class _TwoLaneRepository implements BoardRepository {
  @override
  Future<List<Space>> loadSpaces() =>
      Future.value(const [Space(id: 'home', name: 'Home')]);

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) =>
      Future.value(
        BoardData(
          statuses: const [
            WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
            WorkItemStatus(id: 'done', name: 'Done', order: 1),
          ],
          swimlanes: [
            SwimlaneItems(
              lane: workItem('lane-a', title: 'Lane A'),
              cards: [workItem('card-a', parentId: 'lane-a')],
            ),
            SwimlaneItems(
              lane: workItem('lane-b', title: 'Lane B'),
              cards: [workItem('card-b', parentId: 'lane-b')],
            ),
          ],
        ),
      );

  @override
  Future<List<User>> loadUsers() => Future.value(const []);

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

/// The board as [BoardView] wires it: rebuilt whole when the view model
/// notifies, with each lane listening for itself.
Widget _board(BoardViewModel viewModel) => MaterialApp(
  home: Scaffold(
    body: SingleChildScrollView(
      child: ListenableBuilder(
        listenable: viewModel,
        builder: (context, _) => SwimlaneView(
          width: 1280,
          statuses: viewModel.visibleStatuses,
          swimlanes: viewModel.swimlaneListenables,
          onCardDropped: (_, _, {beforeCardId}) async {},
          onCardReparented: (_, _) async {},
          onCardDetailsOpened: (_) {},
          onSwimlaneLabelTapped: (_) {},
          onAssignRequested: (_, _) {},
          onAddTagsRequested: (_, _) {},
          cardVisible: viewModel.cardVisible,
          cardComparator: viewModel.cardComparator,
          assigneeInitialFor: viewModel.assigneeInitialFor,
          layerColorFor: viewModel.layerColorFor,
          collapsedSwimlaneIds: const {},
          onToggleSwimlaneCollapsed: (_) {},
          onToggleAllSwimlanesCollapsed: () {},
        ),
      ),
    ),
  ),
);

void main() {
  testWidgets('editing one card rebuilds only its own lane', (tester) async {
    tester.view.physicalSize = const Size(1280, 1600);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);

    final items = WorkItemStore();
    final viewModel = BoardViewModel(
      _TwoLaneRepository(),
      items,
      MemoryLastSpaceStore(),
    );
    addTearDown(viewModel.dispose);
    await viewModel.load();
    await tester.pumpWidget(_board(viewModel));

    final columnBuildsByLane = <String, int>{};
    widgets.debugOnRebuildDirtyWidget = (element, _) {
      if (element.widget case final StatusColumn column) {
        final lane = column.swimlane.parentId;
        columnBuildsByLane[lane] = (columnBuildsByLane[lane] ?? 0) + 1;
      }
    };
    addTearDown(() => widgets.debugOnRebuildDirtyWidget = null);

    await items.write(
      'card-a',
      send: (version) async => withVersion(
        workItem('card-a', title: 'Renamed', parentId: 'lane-a'),
        version + 1,
      ),
      reload: () => throw StateError('No conflict expected'),
    );
    await tester.pump();

    expect(find.text('Renamed'), findsOneWidget);
    expect(columnBuildsByLane['lane-a'], greaterThan(0));
    expect(columnBuildsByLane['lane-b'], isNull);
  });
}
