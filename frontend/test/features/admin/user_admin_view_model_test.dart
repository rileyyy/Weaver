import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';

import 'fakes/fake_user_admin_repository.dart';

class _SignedInAs implements CurrentUser {
  const _SignedInAs(this.currentUserId);

  @override
  final String? currentUserId;
}

void main() {
  late FakeUserAdminRepository repository;
  late UserAdminViewModel viewModel;

  setUp(() async {
    repository = FakeUserAdminRepository([
      adminUser('admin', isAdmin: true),
      adminUser('carol'),
    ]);
    viewModel = UserAdminViewModel(repository, const _SignedInAs('admin'));
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test('knows which user is signed in', () {
    expect(viewModel.isCurrentUser(viewModel.users[0]), isTrue);
    expect(viewModel.isCurrentUser(viewModel.users[1]), isFalse);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);
    final vm = UserAdminViewModel(repository, const _SignedInAs('admin'));
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('create adds the user in username order', () async {
    final error = await viewModel.create(
      const NewUser(
        username: 'Bob',
        password: 'a long password',
        isAdmin: false,
      ),
    );

    expect(error, isNull);
    expect(viewModel.users.map((u) => u.username), ['admin', 'Bob', 'carol']);
  });

  test('create with a blank username asks for one without a request', () async {
    final error = await viewModel.create(
      const NewUser(
        username: '  ',
        password: 'a long password',
        isAdmin: false,
      ),
    );

    expect(error, 'Enter a username.');
    expect(repository.calls, isEmpty);
  });

  test('create reports the server validation message', () async {
    repository.error = const ApiException(
      'Password must be at least 12 characters long (400).',
      statusCode: 400,
    );

    final error = await viewModel.create(
      const NewUser(username: 'bob', password: 'short', isAdmin: false),
    );

    expect(error, contains('at least 12'));
  });

  test('rename replaces the user and keeps username order', () async {
    await viewModel.rename('carol', 'aaron');

    expect(viewModel.users.map((u) => u.username), ['aaron', 'admin']);
  });

  test(
    'setActive and setAdmin replace the user with the server answer',
    () async {
      await viewModel.setActive('carol', isActive: false);
      expect(viewModel.users[1].isActive, isFalse);

      await viewModel.setAdmin('carol', isAdmin: true);
      expect(viewModel.users[1].isAdmin, isTrue);
    },
  );

  test('unlock and resetPassword call through', () async {
    await viewModel.unlock('carol');
    await viewModel.resetPassword('carol', 'a long password');

    expect(repository.calls, ['unlock carol', 'resetPassword carol']);
  });

  test('delete removes the user', () async {
    final error = await viewModel.delete('carol');

    expect(error, isNull);
    expect(viewModel.users.map((u) => u.id), ['admin']);
  });

  test('a refused delete keeps the user and shows why', () async {
    repository.error = const ApiException(
      "User 'carol' has written 2 comment(s) (409).",
      statusCode: 409,
    );

    final error = await viewModel.delete('carol');

    expect(error, contains('comment'));
    expect(viewModel.users, hasLength(2));
  });

  test('an unreachable server gets a generic message', () async {
    repository.error = const ApiException('boom', statusCode: 502);

    final error = await viewModel.setActive('carol', isActive: false);

    expect(error, 'Could not deactivate the user. Try again.');
  });
}
