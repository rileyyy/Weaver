import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/backlog_view_model.dart';
import 'package:weaver/features/board/views/backlog/backlog_view.dart';
import 'package:weaver/shared/data/work_item_store.dart';

import '../../../../shared/fakes/work_items.dart';
import '../../fakes/fake_backlog_repository.dart';

final _nov = DateTime(2026, 11, 14);

void main() {
  late FakeBacklogRepository repository;
  late BacklogViewModel viewModel;
  final opened = <String>[];

  setUp(() async {
    opened.clear();
    repository = FakeBacklogRepository([
      workItem('shed', number: 3, title: 'Shed', startDate: _nov),
      workItem('roof', number: 4, parentId: 'shed', title: 'Fix roof'),
      workItem('bench', number: 5, title: 'Garden bench'),
    ]);
    viewModel = BacklogViewModel(repository, WorkItemStore());
    await viewModel.load('home');
  });

  tearDown(() => viewModel.dispose());

  Future<void> pump(WidgetTester tester, {DateTimeRange? chosen}) =>
      tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: BacklogView(
              viewModel: viewModel,
              spaceId: 'home',
              onItemOpened: opened.add,
              chooseDates: (_, _) async => chosen,
            ),
          ),
        ),
      );

  testWidgets('shows each item under its parent', (tester) async {
    await pump(tester);

    expect(find.text('Top level'), findsOneWidget);
    expect(find.text('#5 Garden bench'), findsOneWidget);
    expect(find.text('#3 Shed'), findsOneWidget);
    expect(find.text('#4 Fix roof'), findsOneWidget);
  });

  testWidgets('setting dates takes the item out of the Backlog', (
    tester,
  ) async {
    await pump(
      tester,
      chosen: DateTimeRange(start: _nov, end: _nov),
    );

    await tester.tap(find.text('Set dates').last);
    await tester.pumpAndSettle();

    expect(repository.server['roof']!.startDate, _nov);
    expect(find.text('#4 Fix roof'), findsNothing);
    expect(find.text('#5 Garden bench'), findsOneWidget);
  });

  testWidgets('cancelling the picker changes nothing', (tester) async {
    await pump(tester);

    await tester.tap(find.text('Set dates').first);
    await tester.pumpAndSettle();

    expect(repository.reschedules, isEmpty);
  });

  testWidgets('a failed save says so', (tester) async {
    repository.rescheduleError = const ApiException('down');
    await pump(
      tester,
      chosen: DateTimeRange(start: _nov, end: _nov),
    );

    await tester.tap(find.text('Set dates').first);
    await tester.pumpAndSettle();

    expect(find.text('Could not set the dates. Try again.'), findsOneWidget);
    expect(find.text('#5 Garden bench'), findsOneWidget);
  });

  testWidgets('tapping an item or its parent opens it', (tester) async {
    await pump(tester);

    await tester.tap(find.text('#4 Fix roof'));
    await tester.tap(find.text('#3 Shed'));

    expect(opened, ['roof', 'shed']);
  });

  testWidgets('an empty Backlog says so', (tester) async {
    repository.done.addAll(['roof', 'bench']);
    await viewModel.load('home');
    await pump(tester);

    expect(find.textContaining('The Backlog is empty'), findsOneWidget);
  });
}
