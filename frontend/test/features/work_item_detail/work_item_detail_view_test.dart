import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

class _NoUser implements CurrentUser {
  @override
  String? get currentUserId => null;
}

/// Serves just what the detail view loads; anything else is unexpected.
class _LoadOnlyRepository implements WorkItemDetailRepository {
  @override
  Future<WorkItemDetail> getItem(String id) async => WorkItemDetail(
    id: id,
    parentId: null,
    title: 'Original title',
    description: null,
    statusId: 'todo',
    layerId: null,
    priority: WorkItemPriority.medium,
    assignedToUserId: null,
    startDate: null,
    endDate: null,
    createdAt: DateTime(2026, 9, 1),
    updatedAt: DateTime(2026, 9, 1),
    version: 1,
  );

  @override
  Future<List<WorkItemStatus>> loadStatuses() async => const [
    WorkItemStatus(id: 'todo', name: 'To Do', order: 0),
  ];

  @override
  Future<List<WorkItemLayer>> loadLayers() async => const [];

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  Future<List<WorkItemComment>> loadComments(String workItemId) async =>
      const [];

  @override
  Future<List<WorkItemLink>> loadLinks(String workItemId) async => const [];

  @override
  Future<List<WorkItemChildSummary>> loadChildren(String parentId) async =>
      const [];

  @override
  dynamic noSuchMethod(Invocation invocation) =>
      throw UnimplementedError('${invocation.memberName}');
}

Future<void> _openDetails(WidgetTester tester) async {
  tester.view.physicalSize = const Size(1200, 1600);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () => Navigator.of(context).push(
            MaterialPageRoute<void>(
              builder: (_) => WorkItemDetailView(
                workItemId: 'item-1',
                viewModel: WorkItemDetailViewModel(
                  _LoadOnlyRepository(),
                  _NoUser(),
                ),
              ),
            ),
          ),
          child: const Text('open'),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

Finder get _titleField => find.widgetWithText(TextField, 'Original title');

void main() {
  testWidgets('closing without edits needs no confirmation', (tester) async {
    await _openDetails(tester);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('Work Item Details'), findsNothing);
  });

  testWidgets('closing with an unsaved title asks before discarding it', (
    tester,
  ) async {
    await _openDetails(tester);
    await tester.enterText(_titleField, 'Edited title');
    await tester.pump();
    expect(find.text('Unsaved changes'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('Discard unsaved changes?'), findsOneWidget);

    await tester.tap(find.text('Keep editing'));
    await tester.pumpAndSettle();
    expect(find.text('Edited title'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tester.tap(find.text('Discard'));
    await tester.pumpAndSettle();
    expect(find.text('Work Item Details'), findsNothing);
  });

  testWidgets('a blank title is flagged and cannot be saved', (tester) async {
    await _openDetails(tester);

    await tester.enterText(_titleField, '');
    await tester.pump();

    expect(find.text('Title is required.'), findsOneWidget);
    final save = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Save details'),
    );
    expect(save.onPressed, isNull);
  });
}
