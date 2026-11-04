import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/custom_field_admin_view_model.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/custom_field_option.dart';

import 'fakes/fake_custom_field_admin_repository.dart';

const _budget = CustomField(
  id: 'field-budget',
  typeId: 'type-room',
  name: 'Budget',
  kind: CustomFieldKind.currency,
  order: 0,
);

const _floor = CustomField(
  id: 'field-floor',
  typeId: 'type-room',
  name: 'Floor',
  kind: CustomFieldKind.singleSelect,
  order: 1,
  options: [CustomFieldOption(id: 'option-oak', name: 'Oak')],
);

void main() {
  late FakeCustomFieldAdminRepository repository;
  late CustomFieldAdminViewModel viewModel;

  setUp(() async {
    repository = FakeCustomFieldAdminRepository([_budget, _floor]);
    viewModel = CustomFieldAdminViewModel(repository, 'type-room');
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test("lists the Type's fields", () {
    expect(repository.calls, ['list type-room']);
    expect(viewModel.items.map((f) => f.name), ['Budget', 'Floor']);
  });

  test('a load failure says so', () async {
    final failing = _FailingListRepository();
    final vm = CustomFieldAdminViewModel(failing, 'type-room');
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('create appends the field with its kind and options', () async {
    final error = await viewModel.create('Wood', CustomFieldKind.multiSelect, [
      'Oak',
      'Pine',
    ]);

    expect(error, isNull);
    expect(
      repository.calls.last,
      'create Wood MultiSelect [Oak, Pine] in type-room',
    );
    expect(viewModel.items.map((f) => f.name), ['Budget', 'Floor', 'Wood']);
  });

  test('update renames the field and revises its options', () async {
    final error = await viewModel.update('field-floor', 'Flooring', const [
      CustomFieldOptionDraft(id: 'option-oak', name: 'Oak'),
      CustomFieldOptionDraft(name: 'Tiles'),
    ]);

    expect(error, isNull);
    expect(
      repository.calls.last,
      'update field-floor Flooring [option-oak:Oak, null:Tiles]',
    );
    expect(viewModel.items.last.options.map((o) => o.name), ['Oak', 'Tiles']);
  });

  test('move shows the new order and saves it', () async {
    final error = await viewModel.move(1, 0);

    expect(error, isNull);
    expect(
      repository.calls.last,
      'reorder type-room [field-floor, field-budget]',
    );
    expect(viewModel.items.map((f) => f.name), ['Floor', 'Budget']);
  });

  test('a failed move puts the old order back and says why', () async {
    repository.error = const ApiException('Nope', statusCode: 400);

    final error = await viewModel.move(1, 0);

    expect(error, 'Nope');
    expect(viewModel.items.map((f) => f.name), ['Budget', 'Floor']);
  });

  test('delete drops the field from the list', () async {
    final error = await viewModel.delete('field-budget');

    expect(error, isNull);
    expect(viewModel.items.map((f) => f.name), ['Floor']);
  });

  test("a refused change returns the server's reason", () async {
    repository.error = const ApiException(
      'Field "Budget" already exists.',
      statusCode: 409,
    );

    final error = await viewModel.create('Budget', CustomFieldKind.number, []);

    expect(error, 'Field "Budget" already exists.');
    expect(viewModel.items, hasLength(2));
  });
}

class _FailingListRepository extends FakeCustomFieldAdminRepository {
  _FailingListRepository() : super(const []);

  @override
  Future<List<CustomField>> list(String typeId) async =>
      throw const ApiException('down');
}
