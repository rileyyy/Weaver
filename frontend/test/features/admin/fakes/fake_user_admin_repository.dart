import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/user_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/shared/models/user_kind.dart';

AdminUser adminUser(
  String id, {
  String? username,
  bool isAdmin = false,
  bool isActive = true,
  DateTime? lockedUntil,
}) => AdminUser(
  id: id,
  username: username ?? id,
  kind: UserKind.human,
  isAdmin: isAdmin,
  isActive: isActive,
  lockedUntil: lockedUntil,
);

class FakeUserAdminRepository implements UserAdminRepository {
  FakeUserAdminRepository(this.users);

  List<AdminUser> users;
  ApiException? error;
  final List<String> calls = [];

  Future<AdminUser> _changed(
    String call,
    String id,
    AdminUser Function(AdminUser user) change,
  ) async {
    calls.add(call);
    final error = this.error;
    if (error != null) throw error;
    return change(users.firstWhere((user) => user.id == id));
  }

  @override
  Future<List<AdminUser>> list() async {
    final error = this.error;
    if (error != null) throw error;
    return users;
  }

  @override
  Future<AdminUser> create(NewUser user) async {
    calls.add('create ${user.username}');
    final error = this.error;
    if (error != null) throw error;
    return adminUser('new', username: user.username, isAdmin: user.isAdmin);
  }

  @override
  Future<AdminUser> rename(String id, String username) => _changed(
    'rename $id $username',
    id,
    (user) => adminUser(id, username: username, isAdmin: user.isAdmin),
  );

  @override
  Future<AdminUser> resetPassword(String id, String password) =>
      _changed('resetPassword $id', id, (user) => user);

  @override
  Future<AdminUser> unlock(String id) => _changed(
    'unlock $id',
    id,
    (user) => adminUser(id, username: user.username),
  );

  @override
  Future<AdminUser> setAdmin(String id, {required bool isAdmin}) => _changed(
    'setAdmin $id $isAdmin',
    id,
    (user) => adminUser(id, username: user.username, isAdmin: isAdmin),
  );

  @override
  Future<AdminUser> setActive(String id, {required bool isActive}) => _changed(
    'setActive $id $isActive',
    id,
    (user) => adminUser(id, username: user.username, isActive: isActive),
  );

  @override
  Future<void> delete(String id) async {
    calls.add('delete $id');
    final error = this.error;
    if (error != null) throw error;
  }
}
