import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/trash_view_model.dart';
import 'package:weaver/features/board/views/trash/trash_view.dart';
import 'package:weaver/shared/models/user_kind.dart';

import '../../fakes/fake_trash_repository.dart';

Future<TrashViewModel> _pump(
  WidgetTester tester,
  FakeTrashRepository repository, {
  List<void>? restored,
}) async {
  final viewModel = TrashViewModel(repository);
  addTearDown(viewModel.dispose);
  await viewModel.load();
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: TrashView(
          viewModel: viewModel,
          onRestored: () => restored?.add(null),
        ),
      ),
    ),
  );
  return viewModel;
}

void main() {
  testWidgets('lists each delete with where, when, who and how many items', (
    tester,
  ) async {
    await _pump(
      tester,
      FakeTrashRepository([
        trashed(
          number: 12,
          title: 'Old shed',
          trashedAt: DateTime(2026, 11, 4, 9, 5),
          itemCount: 3,
          deletedByName: 'claude',
          deletedByKind: UserKind.agent,
          deletedByActorLabel: 'tidy-up',
        ),
        trashed(
          workItemId: 'item-2',
          number: 13,
          title: 'Typo',
          deletedByName: null,
          deletedByKind: null,
        ),
      ]),
    );

    expect(find.text('#12 Old shed'), findsOneWidget);
    expect(
      find.text(
        'Home · Deleted 2026-11-04 09:05 by claude (agent, tidy-up) · 3 items',
      ),
      findsOneWidget,
    );
    expect(find.text('#13 Typo'), findsOneWidget);
    expect(find.text('Home · Deleted 2026-11-04 09:00'), findsOneWidget);
  });

  testWidgets('says so when the Trash is empty, with nothing to empty', (
    tester,
  ) async {
    await _pump(tester, FakeTrashRepository());

    expect(find.text('The Trash is empty.'), findsOneWidget);
    expect(find.text('Empty Trash'), findsNothing);
  });

  testWidgets('restoring an item takes it off the list and reports it', (
    tester,
  ) async {
    final repository = FakeTrashRepository([trashed(title: 'Old shed')]);
    final restored = <void>[];
    await _pump(tester, repository, restored: restored);

    await tester.tap(find.widgetWithText(TextButton, 'Restore'));
    await tester.pumpAndSettle();

    expect(repository.restoreCalls, ['item-1']);
    expect(restored, hasLength(1));
    expect(find.text('#1 Old shed'), findsNothing);
  });

  testWidgets('a failed restore keeps the item and shows why', (tester) async {
    final repository = FakeTrashRepository([trashed()]);
    final restored = <void>[];
    await _pump(tester, repository, restored: restored);
    repository.error = const ApiException(
      'Restore its parent.',
      statusCode: 400,
    );

    await tester.tap(find.widgetWithText(TextButton, 'Restore'));
    await tester.pumpAndSettle();

    expect(find.text('Restore its parent.'), findsOneWidget);
    expect(restored, isEmpty);
    expect(find.text('#1 Old shed'), findsOneWidget);
  });

  testWidgets('deleting permanently asks first, naming the item and count', (
    tester,
  ) async {
    final repository = FakeTrashRepository([
      trashed(number: 12, title: 'Old shed', itemCount: 3),
    ]);
    await _pump(tester, repository);

    await tester.tap(find.byTooltip('Delete permanently'));
    await tester.pumpAndSettle();

    final dialog = find.byType(AlertDialog);
    expect(
      find.descendant(
        of: dialog,
        matching: find.textContaining('#12 Old shed'),
      ),
      findsWidgets,
    );
    expect(
      find.descendant(of: dialog, matching: find.textContaining('3 items')),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: dialog,
        matching: find.textContaining("can't be undone"),
      ),
      findsOneWidget,
    );
    expect(repository.purgeCalls, isEmpty);
  });

  testWidgets('confirming a permanent delete takes the item off the list', (
    tester,
  ) async {
    final repository = FakeTrashRepository([trashed(title: 'Old shed')]);
    await _pump(tester, repository);

    await tester.tap(find.byTooltip('Delete permanently'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete permanently'));
    await tester.pumpAndSettle();

    expect(repository.purgeCalls, ['item-1']);
    expect(find.text('#1 Old shed'), findsNothing);
    expect(find.text('The Trash is empty.'), findsOneWidget);
  });

  testWidgets('cancelling a permanent delete keeps the item', (tester) async {
    final repository = FakeTrashRepository([trashed()]);
    await _pump(tester, repository);

    await tester.tap(find.byTooltip('Delete permanently'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(repository.purgeCalls, isEmpty);
    expect(find.text('#1 Old shed'), findsOneWidget);
  });

  testWidgets('a failed permanent delete keeps the item and shows why', (
    tester,
  ) async {
    final repository = FakeTrashRepository([trashed()]);
    await _pump(tester, repository);
    repository.error = const ApiException(
      'Purge its parent instead.',
      statusCode: 400,
    );

    await tester.tap(find.byTooltip('Delete permanently'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete permanently'));
    await tester.pumpAndSettle();

    expect(find.text('Purge its parent instead.'), findsOneWidget);
    expect(find.text('#1 Old shed'), findsOneWidget);
  });

  testWidgets('emptying the Trash asks first and then clears the list', (
    tester,
  ) async {
    final repository = FakeTrashRepository([
      trashed(workItemId: 'a'),
      trashed(workItemId: 'b', number: 2),
    ]);
    await _pump(tester, repository);

    await tester.tap(find.text('Empty Trash'));
    await tester.pumpAndSettle();
    expect(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.textContaining("can't be undone"),
      ),
      findsOneWidget,
    );
    expect(repository.emptyCalls, 0);

    await tester.tap(find.widgetWithText(FilledButton, 'Empty Trash'));
    await tester.pumpAndSettle();

    expect(repository.emptyCalls, 1);
    expect(find.text('The Trash is empty.'), findsOneWidget);
  });

  testWidgets('a failed empty keeps the list and shows why', (tester) async {
    final repository = FakeTrashRepository([trashed()]);
    await _pump(tester, repository);
    repository.error = const ApiException('down', statusCode: 503);

    await tester.tap(find.text('Empty Trash'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Empty Trash'));
    await tester.pumpAndSettle();

    expect(find.text('Could not empty the Trash. Try again.'), findsOneWidget);
    expect(find.text('#1 Old shed'), findsOneWidget);
  });
}
