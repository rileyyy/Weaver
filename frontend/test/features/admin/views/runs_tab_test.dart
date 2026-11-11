import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
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
}
