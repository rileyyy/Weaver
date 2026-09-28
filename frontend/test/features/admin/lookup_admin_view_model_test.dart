import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

import 'fakes/fake_lookup_admin_repository.dart';

void main() {
  late FakeLookupAdminRepository repository;
  late LookupAdminViewModel viewModel;

  setUp(() async {
    repository = FakeLookupAdminRepository(const [
      LookupValue(id: 'low', name: 'Low', order: 0),
      LookupValue(id: 'high', name: 'High', order: 1),
    ]);
    viewModel = LookupAdminViewModel(repository, LookupKind.priorities);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test('loads the values of its kind', () {
    expect(repository.calls.single, 'list priorities');
    expect(viewModel.items.map((v) => v.name), ['Low', 'High']);
  });

  test('create and rename go to its kind', () async {
    await viewModel.create('Urgent');
    await viewModel.rename('low', 'Minor');

    expect(repository.calls.skip(1), [
      'create priorities Urgent',
      'rename priorities low Minor',
    ]);
    expect(viewModel.items.map((v) => v.name), ['Minor', 'High', 'Urgent']);
  });

  test('delete without a replacement passes none', () async {
    await viewModel.delete('high');

    expect(repository.calls.last, 'delete priorities high -> null');
    expect(viewModel.items.map((v) => v.id), ['low']);
  });

  test('move saves the new order', () async {
    await viewModel.move(1, 0);

    expect(viewModel.items.map((v) => v.id), ['high', 'low']);
  });
}
