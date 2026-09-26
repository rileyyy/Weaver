import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_view.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

class _HierarchyOnlyRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() async => null;

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async => const BoardData(
    statuses: [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
    swimlanes: [],
  );

  @override
  Future<List<HierarchyItem>> loadAllItems() async => const [
    HierarchyItem(
      id: 'root',
      number: 1,
      parentId: null,
      title: 'A root item with a fairly long title',
      statusId: 'todo',
      tags: ['urgent', 'design'],
    ),
    HierarchyItem(
      id: 'child',
      number: 2,
      parentId: 'root',
      title: 'Child',
      statusId: 'todo',
    ),
  ];

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

void main() {
  testWidgets('a narrow screen scrolls horizontally instead of overflowing', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(400, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    final viewModel = BoardViewModel(_HierarchyOnlyRepository());
    await viewModel.load();
    await viewModel.loadHierarchy();

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: HierarchyView(
            viewModel: viewModel,
            onItemOpened: (_) {},
            onAssignRequested: (_, _) {},
          ),
        ),
      ),
    );

    // A RenderFlex overflow would be reported as a test failure here.
    expect(find.text('Child'), findsOneWidget);
    final scrollable = tester.widget<SingleChildScrollView>(
      find.byType(SingleChildScrollView),
    );
    expect(scrollable.scrollDirection, Axis.horizontal);

    await tester.drag(find.text('Child'), const Offset(-300, 0));
    await tester.pump();
    expect(tester.takeException(), isNull);
  });
}
