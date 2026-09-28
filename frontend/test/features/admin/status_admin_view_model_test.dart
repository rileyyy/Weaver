import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';

import 'fakes/fake_status_admin_repository.dart';

const _review = StatusDetails(
  name: 'Review',
  category: StatusCategory.doing,
  color: '#00897B',
);

void main() {
  late FakeStatusAdminRepository repository;
  late StatusAdminViewModel viewModel;

  setUp(() async {
    repository = FakeStatusAdminRepository([
      adminStatus('todo'),
      adminStatus('doing'),
      adminStatus('done'),
    ]);
    viewModel = StatusAdminViewModel(repository);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test('load lists the statuses', () {
    expect(viewModel.items.map((s) => s.id), ['todo', 'doing', 'done']);
    expect(viewModel.isLoading, isFalse);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);

    final vm = StatusAdminViewModel(repository);
    addTearDown(vm.dispose);
    await vm.load();

    expect(vm.loadError, contains('statuses'));
  });

  test(
    'move shows the new order straight away and keeps it once saved',
    () async {
      repository.reorderGate = Completer<void>();

      final move = viewModel.move(2, 0);
      expect(viewModel.items.map((s) => s.id), ['done', 'todo', 'doing']);
      repository.reorderGate!.complete();
      final error = await move;

      expect(error, isNull);
      expect(repository.calls.single, 'reorder done,todo,doing');
      expect(viewModel.items.map((s) => s.id), ['done', 'todo', 'doing']);
    },
  );

  test('a failed move puts the old order back and says why', () async {
    repository.error = const ApiException(
      'Not an admin (403).',
      statusCode: 403,
    );

    final error = await viewModel.move(0, 2);

    expect(error, 'Not an admin (403).');
    expect(viewModel.items.map((s) => s.id), ['todo', 'doing', 'done']);
  });

  test('create appends the new status', () async {
    final error = await viewModel.create(_review);

    expect(error, isNull);
    expect(viewModel.items.last.name, 'Review');
  });

  test('create reports a conflict from the server', () async {
    repository.error = const ApiException(
      'A status named Review already exists (409).',
      statusCode: 409,
    );

    final error = await viewModel.create(_review);

    expect(error, contains('already exists'));
    expect(viewModel.items, hasLength(3));
  });

  test('update replaces the status in place', () async {
    await viewModel.update('doing', _review);

    expect(viewModel.items.map((s) => s.name), ['todo', 'Review', 'done']);
  });

  test('delete passes the replacement and removes the status', () async {
    final error = await viewModel.delete('doing', replacementId: 'todo');

    expect(error, isNull);
    expect(repository.calls.single, 'delete doing -> todo');
    expect(viewModel.items.map((s) => s.id), ['todo', 'done']);
  });

  test(
    'a failed delete keeps the status and falls back to a generic message',
    () async {
      repository.error = const ApiException('boom', statusCode: 500);

      final error = await viewModel.delete('doing', replacementId: 'todo');

      expect(error, 'Could not delete the status. Try again.');
      expect(viewModel.items, hasLength(3));
    },
  );

  test('the last status cannot be deleted', () async {
    expect(viewModel.canDelete, isTrue);

    await viewModel.delete('doing', replacementId: 'todo');
    await viewModel.delete('done', replacementId: 'todo');

    expect(viewModel.canDelete, isFalse);
  });
}
