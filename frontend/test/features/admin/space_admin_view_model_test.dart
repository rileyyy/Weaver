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

  test('name order ignores case, as the server lists them', () async {
    await viewModel.create('allotment');

    expect(names(), ['allotment', 'Home', 'Village']);
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

  group('setDoneHidingPeriod', () {
    test('saves the trimmed days and shows the new period', () async {
      final error = await viewModel.setDoneHidingPeriod('home', ' 30 ');

      expect(error, isNull);
      expect(repository.calls.last, 'setDoneHidingPeriod home 30');
      expect(viewModel.spaces.first.doneHidingDays, 30);
    });

    test('accepts both ends of the range', () async {
      expect(await viewModel.setDoneHidingPeriod('home', '1'), isNull);
      expect(await viewModel.setDoneHidingPeriod('home', '3650'), isNull);
    });

    for (final typed in ['0', '3651', '-5', 'two weeks', '1.5', '']) {
      test('refuses "$typed" without a request', () async {
        final error = await viewModel.setDoneHidingPeriod('home', typed);

        expect(error, 'Enter a whole number of days from 1 to 3650.');
        expect(repository.calls, ['list']);
      });
    }

    test("shows the server's reason on failure and keeps the period", () async {
      repository.error = const ApiException('Not an admin.', statusCode: 403);

      final error = await viewModel.setDoneHidingPeriod('home', '30');

      expect(error, 'Not an admin.');
      expect(viewModel.spaces.first.doneHidingDays, 14);
    });
  });
}
