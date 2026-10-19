import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_layout.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_view.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_layer.dart';
import 'package:weaver/shared/models/work_item_status.dart';

const _projectColor = Color(0xFFD81B60);

class _HierarchyOnlyRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() async => null;

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async => const BoardData(
    statuses: [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
    swimlanes: [],
    layers: [
      WorkItemLayer(
        id: 'project',
        name: 'Project',
        order: 0,
        color: _projectColor,
      ),
    ],
  );

  @override
  Future<List<HierarchyItem>> loadAllItems() async => const [
    HierarchyItem(
      id: 'root',
      number: 1,
      parentId: null,
      title: 'A root item with a fairly long title',
      statusId: 'todo',
      layerId: 'project',
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
  testWidgets("a title is underlined in its layer's colour", (tester) async {
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
            onAddTagsRequested: (_, _) {},
          ),
        ),
      ),
    );

    final root = tester.widget<Text>(
      find.text('A root item with a fairly long title'),
    );
    final child = tester.widget<Text>(find.text('Child'));
    expect(root.style?.decoration, TextDecoration.underline);
    expect(root.style?.decorationColor, _projectColor);
    expect(child.style?.decoration, isNull);
  });

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
            onAddTagsRequested: (_, _) {},
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

  testWidgets(
    'the caret indents with the title while other columns stay aligned',
    (tester) async {
      tester.view.physicalSize = const Size(1400, 800);
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
              onAddTagsRequested: (_, _) {},
            ),
          ),
        ),
      );

      // Only the root has children, so it has the only caret; the child's
      // title must start one indent level further right than the root's.
      final caretX = tester.getTopLeft(find.byIcon(Icons.expand_more)).dx;
      final rootTitleX = tester
          .getTopLeft(find.text('A root item with a fairly long title'))
          .dx;
      final childTitleX = tester.getTopLeft(find.text('Child')).dx;
      final statusXs = tester
          .widgetList(find.text('To Do'))
          .map((w) => tester.getTopLeft(find.byWidget(w)).dx)
          .toList();
      final titleHeaderX = tester.getTopLeft(find.text('Title')).dx;

      expect(caretX, lessThan(rootTitleX));
      expect(caretX, greaterThanOrEqualTo(titleHeaderX));
      expect(childTitleX - rootTitleX, hierarchyIndentPerLevel);
      expect(statusXs, hasLength(2));
      expect(statusXs.first, statusXs.last);
    },
  );
}
