import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_history_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';
import 'package:weaver/features/work_item_detail/work_item_history_view_model.dart';

WorkItemHistoryEntry _entry(int id) => WorkItemHistoryEntry(
  id: id,
  changedAt: DateTime(2026, 9, 1),
  accountName: 'alice',
  accountKind: null,
  apiKeyLabel: null,
  actorLabel: null,
  field: 'title',
  oldValue: 'a',
  newValue: 'b',
  oldName: null,
  newName: null,
);

class _FakeHistoryRepository implements WorkItemHistoryRepository {
  List<WorkItemHistoryEntry> entries = [];
  bool fail = false;
  int loads = 0;

  @override
  Future<List<WorkItemHistoryEntry>> loadHistory(String workItemId) async {
    loads++;
    if (fail) throw const ApiException('boom');
    return entries;
  }
}

void main() {
  test('load fetches the entries', () async {
    final repository = _FakeHistoryRepository()
      ..entries = [_entry(2), _entry(1)];
    final viewModel = WorkItemHistoryViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.isLoading, isFalse);
    expect(viewModel.entries.map((e) => e.id), [2, 1]);
    expect(viewModel.loadError, isNull);
  });

  test('a failed load shows an error and keeps nothing stale', () async {
    final repository = _FakeHistoryRepository()..entries = [_entry(1)];
    final viewModel = WorkItemHistoryViewModel(repository);
    await viewModel.load('item-1');

    repository.fail = true;
    await viewModel.load('item-1');

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.entries, isEmpty);
  });

  test('loading again refreshes, so changes made since show up', () async {
    final repository = _FakeHistoryRepository()..entries = [_entry(1)];
    final viewModel = WorkItemHistoryViewModel(repository);
    await viewModel.load('item-1');

    repository.entries = [_entry(2), _entry(1)];
    await viewModel.load('item-1');

    expect(viewModel.entries.map((e) => e.id), [2, 1]);
    expect(repository.loads, 2);
  });
}
