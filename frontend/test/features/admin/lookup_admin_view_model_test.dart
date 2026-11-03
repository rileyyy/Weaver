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
    await viewModel.showSpace('home');
  });

  tearDown(() => viewModel.dispose());

  test("lists the chosen Space's priorities", () {
    expect(repository.calls.single, 'list priorities in home');
    expect(viewModel.items.map((v) => v.name), ['Low', 'High']);
  });

  test('priorities are listed only once a Space is chosen', () async {
    final vm = LookupAdminViewModel(repository, LookupKind.priorities);
    addTearDown(vm.dispose);
    repository.calls.clear();

    await vm.load();

    expect(repository.calls, isEmpty);
    expect(vm.items, isEmpty);
  });

  test('layers are shared by every Space, so no Space is needed', () async {
    final layers = LookupAdminViewModel(repository, LookupKind.layers);
    addTearDown(layers.dispose);

    await layers.load();
    await layers.create('Epic', color: '#546E7A');

    expect(repository.calls.skip(1), [
      'list layers',
      'create layers Epic #546E7A',
    ]);
  });

  test('create and update go to its kind', () async {
    await viewModel.create('Urgent');
    await viewModel.update('low', 'Minor');

    expect(repository.calls.skip(1), [
      'create priorities Urgent null in home',
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

  test('move saves the new order in the chosen Space', () async {
    await viewModel.move(1, 0);

    expect(repository.calls.last, 'reorder priorities high,low in home');
    expect(viewModel.items.map((v) => v.id), ['high', 'low']);
  });
}
