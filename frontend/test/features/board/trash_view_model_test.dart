import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/features/board/trash_view_model.dart';

import 'fakes/fake_trash_repository.dart';

/// Lets a test finish each `list()` call when it chooses.
class _ControlledListRepository extends FakeTrashRepository {
  final List<Completer<List<TrashedWorkItem>>> pending = [];

  @override
  Future<List<TrashedWorkItem>> list() {
    final completer = Completer<List<TrashedWorkItem>>();
    pending.add(completer);
    return completer.future;
  }
}

void main() {
  late FakeTrashRepository repository;
  late TrashViewModel viewModel;

  setUp(() {
    repository = FakeTrashRepository([
      trashed(workItemId: 'a', number: 1),
      trashed(workItemId: 'b', number: 2),
    ]);
    viewModel = TrashViewModel(repository);
  });

  tearDown(() => viewModel.dispose());

  test('load lists every delete in the Trash', () async {
    await viewModel.load();

    expect(viewModel.items.map((t) => t.workItemId), ['a', 'b']);
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);

    await viewModel.load();

    expect(viewModel.loadError, isNotNull);
  });

  test('a slower earlier load cannot overwrite a newer one', () async {
    final controlled = _ControlledListRepository();
    final vm = TrashViewModel(controlled);
    addTearDown(vm.dispose);

    final first = vm.load();
    final second = vm.load();
    controlled.pending[1].complete([trashed(workItemId: 'new')]);
    await second;
    controlled.pending[0].complete([trashed(workItemId: 'old')]);
    await first;

    expect(vm.items.map((t) => t.workItemId), ['new']);
  });

  test('restore takes the item off the list', () async {
    await viewModel.load();

    final restored = await viewModel.restore('a');

    expect(restored, isTrue);
    expect(repository.restoreCalls, ['a']);
    expect(viewModel.items.map((t) => t.workItemId), ['b']);
  });

  test('a restore the server rejects keeps the item and says why', () async {
    await viewModel.load();
    repository.error = const ApiException(
      'Restore its parent instead.',
      statusCode: 400,
    );

    final restored = await viewModel.restore('a');

    expect(restored, isFalse);
    expect(viewModel.items.map((t) => t.workItemId), ['a', 'b']);
    expect(viewModel.actionError, 'Restore its parent instead.');
  });

  test('a restore that fails otherwise gives a generic reason', () async {
    await viewModel.load();
    repository.error = const ApiException('down', statusCode: 503);

    await viewModel.restore('a');

    expect(viewModel.actionError, 'Could not restore this item. Try again.');
    viewModel.clearActionError();
    expect(viewModel.actionError, isNull);
  });

  test('purge takes the item off the list', () async {
    await viewModel.load();

    final purged = await viewModel.purge('a');

    expect(purged, isTrue);
    expect(repository.purgeCalls, ['a']);
    expect(viewModel.items.map((t) => t.workItemId), ['b']);
  });

  test('a purge the server refuses keeps the item and says why', () async {
    await viewModel.load();
    repository.error = const ApiException(
      'Purge its parent instead.',
      statusCode: 400,
    );

    final purged = await viewModel.purge('a');

    expect(purged, isFalse);
    expect(viewModel.items.map((t) => t.workItemId), ['a', 'b']);
    expect(viewModel.actionError, 'Purge its parent instead.');
  });

  test('a forbidden purge says why', () async {
    await viewModel.load();
    repository.error = const ApiException(
      'Agents cannot purge the Trash.',
      statusCode: 403,
    );

    await viewModel.purge('a');

    expect(viewModel.actionError, 'Agents cannot purge the Trash.');
  });

  test('purging an item that is already gone takes it off the list', () async {
    await viewModel.load();
    repository.error = const ApiException('Not found.', statusCode: 404);

    final purged = await viewModel.purge('a');

    expect(purged, isTrue);
    expect(viewModel.items.map((t) => t.workItemId), ['b']);
    expect(viewModel.actionError, isNull);
  });

  test('a purge that fails otherwise gives a generic reason', () async {
    await viewModel.load();
    repository.error = const ApiException('down', statusCode: 503);

    final purged = await viewModel.purge('a');

    expect(purged, isFalse);
    expect(
      viewModel.actionError,
      'Could not delete this item permanently. Try again.',
    );
  });

  test('emptyTrash clears the list', () async {
    await viewModel.load();

    final emptied = await viewModel.emptyTrash();

    expect(emptied, isTrue);
    expect(repository.emptyCalls, 1);
    expect(viewModel.items, isEmpty);
  });

  test('a forbidden emptyTrash keeps the list and says why', () async {
    await viewModel.load();
    repository.error = const ApiException(
      'Agents cannot purge the Trash.',
      statusCode: 403,
    );

    final emptied = await viewModel.emptyTrash();

    expect(emptied, isFalse);
    expect(viewModel.items, hasLength(2));
    expect(viewModel.actionError, 'Agents cannot purge the Trash.');
  });

  test('an emptyTrash that fails otherwise gives a generic reason', () async {
    await viewModel.load();
    repository.error = const ApiException('down', statusCode: 503);

    await viewModel.emptyTrash();

    expect(viewModel.actionError, 'Could not empty the Trash. Try again.');
  });
}
