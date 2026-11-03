import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/shared/models/space.dart';

import 'fakes/fake_space_admin_repository.dart';

void main() {
  late FakeSpaceAdminRepository repository;
  late SpaceAdminViewModel viewModel;

  setUp(() async {
    repository = FakeSpaceAdminRepository(const [
      Space(id: 'home', name: 'Home'),
      Space(id: 'village', name: 'Village'),
    ]);
    viewModel = SpaceAdminViewModel(repository);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  List<String> names() => [for (final space in viewModel.spaces) space.name];

  test('loads the Spaces', () {
    expect(names(), ['Home', 'Village']);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);
    final vm = SpaceAdminViewModel(repository);
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('create adds the Space in name order, trimmed', () async {
    final error = await viewModel.create('  Software ');

    expect(error, isNull);
    expect(repository.calls.last, 'create Software');
    expect(names(), ['Home', 'Software', 'Village']);
  });

  test('create with a blank name asks for one without a request', () async {
    final error = await viewModel.create('   ');

    expect(error, isNotNull);
    expect(repository.calls, ['list']);
  });

  test("create shows the server's reason, e.g. a name already taken", () async {
    repository.error = const ApiException(
      "A Space named 'Home' already exists.",
      statusCode: 409,
    );

    final error = await viewModel.create('home');

    expect(error, "A Space named 'Home' already exists.");
    expect(names(), ['Home', 'Village']);
  });

  test('rename replaces the Space and keeps name order', () async {
    final error = await viewModel.rename('village', 'Allotment');

    expect(error, isNull);
    expect(repository.calls.last, 'rename village Allotment');
    expect(names(), ['Allotment', 'Home']);
  });

  test("rename shows the server's reason on failure", () async {
    repository.error = const ApiException('Not an admin.', statusCode: 403);

    final error = await viewModel.rename('home', 'House');

    expect(error, 'Not an admin.');
    expect(names(), ['Home', 'Village']);
  });
}
