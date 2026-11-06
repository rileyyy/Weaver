import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_layout.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_view.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

import '../../../../shared/fakes/memory_last_space_store.dart';
import '../../../../shared/fakes/work_items.dart';

const _projectColor = Color(0xFFD81B60);

class _HierarchyOnlyRepository implements BoardRepository {
  _HierarchyOnlyRepository({this.customFields = const []});

  /// The Space's fields; when there are any, the root and child hold a
  /// budget.
  final List<CustomField> customFields;

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) async =>
      customFields;

  Map<String, Object?> _budget(num amount) =>
      customFields.isEmpty ? const {} : {'field-budget': amount};

  @override
  Future<List<Space>> loadSpaces() async => const [
    Space(id: 'home', name: 'Home'),
  ];

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async =>
      const BoardData(
        statuses: [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
        swimlanes: [],
        types: [
          WorkItemType(
            id: 'project',
            name: 'Project',
            order: 0,
            color: _projectColor,
          ),
        ],
      );

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) async => [
    workItem(
      'root',
      number: 1,
      parentId: null,
      title: 'A root item with a fairly long title',
      statusId: 'todo',
      typeId: 'project',
      tags: ['urgent', 'design'],
      fields: _budget(1000),
    ),
    workItem(
      'child',
      number: 2,
      parentId: 'root',
      title: 'Child',
      statusId: 'todo',
      fields: _budget(250),
    ),
  ];

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

Future<void> _pumpHierarchy(
  WidgetTester tester,
  _HierarchyOnlyRepository repository,
) async {
  tester.view.physicalSize = const Size(1600, 800);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final viewModel = BoardViewModel(
    repository,
    WorkItemStore(),
    MemoryLastSpaceStore(),
  );
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
}

void main() {
  testWidgets("a Totals column shows each item's own value and its "
      "sub-items' total apart", (tester) async {
    await _pumpHierarchy(
      tester,
      _HierarchyOnlyRepository(
        customFields: const [
          CustomField(
            id: 'field-budget',
            typeId: 'project',
            name: 'Budget',
            kind: CustomFieldKind.currency,
            order: 0,
            rollsUp: true,
          ),
        ],
      ),
    );

    expect(find.text('Totals'), findsOneWidget);
    expect(find.text('Budget 1000.00 · sub-items 250.00'), findsOneWidget);
    expect(find.text('Budget 250.00'), findsOneWidget);
  });

  testWidgets('without a roll-up field there is no Totals column', (
    tester,
  ) async {
    await _pumpHierarchy(tester, _HierarchyOnlyRepository());

    expect(find.text('Totals'), findsNothing);
  });

  testWidgets("a title is underlined in its type's colour", (tester) async {
    final viewModel = BoardViewModel(
      _HierarchyOnlyRepository(),
      WorkItemStore(),
      MemoryLastSpaceStore(),
    );
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
    final viewModel = BoardViewModel(
      _HierarchyOnlyRepository(),
      WorkItemStore(),
      MemoryLastSpaceStore(),
    );
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
      final viewModel = BoardViewModel(
        _HierarchyOnlyRepository(),
        WorkItemStore(),
        MemoryLastSpaceStore(),
      );
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
