import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/run_reports_view_model.dart';

import 'fakes/fake_run_report_repository.dart';

void main() {
  late FakeRunReportRepository repository;
  late RunReportsViewModel viewModel;

  setUp(() {
    repository = FakeRunReportRepository([
      for (var i = 0; i < 3; i++) runReport('run-$i'),
    ], pageSize: 2);
    viewModel = RunReportsViewModel(repository);
  });

  tearDown(() => viewModel.dispose());

  test('load reads the first page', () async {
    await viewModel.load();

    expect(viewModel.runs.map((r) => r.runId), ['run-0', 'run-1']);
    expect(viewModel.hasMore, isTrue);
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
  });

  test('loadMore appends the next page until there are no more', () async {
    await viewModel.load();

    await viewModel.loadMore();

    expect(viewModel.runs.map((r) => r.runId), ['run-0', 'run-1', 'run-2']);
    expect(viewModel.hasMore, isFalse);
    expect(repository.calls, ['list 0', 'list 2']);
  });

  test('a failed load says so', () async {
    repository.error = const ApiException('boom', statusCode: 500);

    await viewModel.load();

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.runs, isEmpty);
  });

  test('a failed loadMore keeps the runs already shown', () async {
    await viewModel.load();
    repository.error = const ApiException('boom', statusCode: 500);

    await viewModel.loadMore();

    expect(viewModel.runs, hasLength(2));
    expect(viewModel.loadMoreError, isNotNull);
    expect(viewModel.hasMore, isTrue);
  });

  test("loadChanges reads a run's changes once", () async {
    repository.changesByRunId['run-0'] = [runChangedItem('Paint the hall')];
    await viewModel.load();
    final run = viewModel.runs.first;

    await viewModel.loadChanges(run);
    await viewModel.loadChanges(run);

    expect(viewModel.changesFor(run)?.items.single.title, 'Paint the hall');
    expect(repository.calls.where((c) => c.startsWith('changes')), [
      'changes run-0',
    ]);
  });

  test('loadChanges tries again after a failure', () async {
    await viewModel.load();
    final run = viewModel.runs.first;
    repository.error = const ApiException('boom', statusCode: 500);
    await viewModel.loadChanges(run);
    expect(viewModel.changesFor(run)?.error, isNotNull);

    repository.error = null;
    await viewModel.loadChanges(run);

    expect(viewModel.changesFor(run)?.error, isNull);
    expect(viewModel.changesFor(run)?.isLoading, isFalse);
  });

  test('a run not yet expanded has no changes state', () async {
    await viewModel.load();

    expect(viewModel.changesFor(viewModel.runs.first), isNull);
  });
}
