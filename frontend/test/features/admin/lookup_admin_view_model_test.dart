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

  test('create and update go to its kind', () async {
    await viewModel.create('Urgent');
    await viewModel.update('low', 'Minor');

    expect(repository.calls.skip(1), [
      'create priorities Urgent null',
      'update priorities low Minor null',
    ]);
    expect(viewModel.items.map((v) => v.name), ['Minor', 'High', 'Urgent']);
  });

  test('a layer is created and updated with its colour', () async {
    final layers = LookupAdminViewModel(
      FakeLookupAdminRepository(const [
        LookupValue(id: 'task', name: 'Task', order: 0, color: '#43A047'),
      ]),
      LookupKind.layers,
    );
    addTearDown(layers.dispose);
    await layers.load();

    await layers.create('Epic', color: '#546E7A');
    await layers.update('task', 'Task', color: '#FDD835');

    expect(layers.items.map((v) => (v.name, v.color)), [
      ('Task', '#FDD835'),
      ('Epic', '#546E7A'),
    ]);
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
