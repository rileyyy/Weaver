import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/run_undo_change.dart';
import 'package:weaver/features/admin/models/run_undo_outcome.dart';
import 'package:weaver/features/admin/models/run_undo_result.dart';
import 'package:weaver/features/admin/run_reports_view_model.dart';
import 'package:weaver/features/admin/views/runs_tab.dart';

import '../fakes/fake_run_report_repository.dart';

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  late FakeRunReportRepository repository;
  late RunReportsViewModel viewModel;

  Future<void> pumpTab(WidgetTester tester) async {
    await viewModel.load();
    await tester.pumpWidget(_host(RunsTab(viewModel: viewModel)));
    await tester.pumpAndSettle();
  }

  setUp(() {
    repository = FakeRunReportRepository(
      [
        runReport(
          'run-1',
          accountName: 'Runner: llama / When',
          at: DateTime(2026, 11, 10, 2, 30),
          changeCount: 2,
        ),
        runReport(
          'run-2',
          accountName: 'Claude: research',
          at: DateTime(2026, 11, 9, 22, 15),
        ),
      ],
      changes: {
        'run-1': [
          runChangedItem(
            'Paint the hall',
            number: 12,
            changes: [
              runChange('when', null, 'next week', actorLabel: 'resolver'),
            ],
          ),
          runChangedItem(
            'Old shed',
            number: null,
            isDeleted: true,
            changes: [runChange('deleted', 'Old shed', null)],
          ),
        ],
      },
    );
    viewModel = RunReportsViewModel(repository);
  });

  tearDown(() => viewModel.dispose());

  testWidgets('shows each run collapsed, with its agent and date', (
    tester,
  ) async {
    await pumpTab(tester);

    expect(find.text('Runner: llama / When'), findsOneWidget);
    expect(find.textContaining('2026-11-10 02:30'), findsOneWidget);
    expect(find.text('Claude: research'), findsOneWidget);
    expect(find.textContaining('2026-11-09 22:15'), findsOneWidget);
    expect(find.text('#12 Paint the hall'), findsNothing);
    expect(repository.calls, isNot(contains('changes run-1')));
  });

  testWidgets(
    'expanding a run shows each item it changed, with the change and the actor label',
    (tester) async {
      await pumpTab(tester);

      await tester.tap(find.text('Runner: llama / When'));
      await tester.pumpAndSettle();

      expect(repository.calls, contains('changes run-1'));
      expect(find.text('#12 Paint the hall'), findsOneWidget);
      expect(find.text('When: none → next week'), findsOneWidget);
      expect(find.text('resolver'), findsOneWidget);
    },
  );

  testWidgets('marks an item the run deleted', (tester) async {
    await pumpTab(tester);

    await tester.tap(find.text('Runner: llama / When'));
    await tester.pumpAndSettle();

    expect(find.text('Old shed (deleted)'), findsOneWidget);
  });

  testWidgets('offers the next page while there is one', (tester) async {
    repository = FakeRunReportRepository([
      for (var i = 0; i < 3; i++) runReport('run-$i', accountName: 'Agent $i'),
    ], pageSize: 2);
    viewModel.dispose();
    viewModel = RunReportsViewModel(repository);
    await pumpTab(tester);
    expect(find.text('Agent 2'), findsNothing);

    await tester.tap(find.text('Load more'));
    await tester.pumpAndSettle();

    expect(find.text('Agent 2'), findsOneWidget);
    expect(find.text('Load more'), findsNothing);
  });

  testWidgets('says so when there are no runs yet', (tester) async {
    repository.runs = [];
    await pumpTab(tester);

    expect(find.textContaining('No runs yet'), findsOneWidget);
  });

  testWidgets('undoing a run asks first, then says what was left alone', (
    tester,
  ) async {
    repository.undoResult = const RunUndoResult([
      RunUndoChange(
        number: 12,
        title: 'Paint the hall',
        field: 'startDate',
        outcome: RunUndoOutcome.undone,
      ),
      RunUndoChange(
        number: 12,
        title: 'Paint the hall',
        field: 'comment',
        outcome: RunUndoOutcome.kept,
      ),
      RunUndoChange(
        number: 12,
        title: 'Paint the hall',
        field: 'when',
        outcome: RunUndoOutcome.changedSince,
      ),
    ]);
    await pumpTab(tester);
    await tester.tap(find.text('Runner: llama / When'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Undo this run'));
    await tester.pumpAndSettle();
    expect(find.text('Undo this run?'), findsOneWidget);
    expect(repository.calls, isNot(contains('undo run-1')));
    await tester.tap(find.text('Undo run'));
    await tester.pumpAndSettle();

    expect(repository.calls, contains('undo run-1'));
    expect(
      find.text('Undone: 1 change. Kept 1 comment. Left 1 as they are:'),
      findsOneWidget,
    );
    expect(
      find.text(
        '#12 Paint the hall, When: not undone, changed by someone since',
      ),
      findsOneWidget,
    );
  });

  testWidgets('cancelling the undo changes nothing', (tester) async {
    await pumpTab(tester);
    await tester.tap(find.text('Runner: llama / When'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Undo this run'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(repository.calls, isNot(contains('undo run-1')));
    expect(find.text('Undo this run'), findsOneWidget);
  });
}
