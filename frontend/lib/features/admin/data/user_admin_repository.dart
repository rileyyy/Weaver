import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/new_user.dart';

/// Account management. Every call needs an admin; the server answers 403
/// otherwise. Each change returns the account as it now is.
abstract class UserAdminRepository {
  Future<List<AdminUser>> list();

  Future<AdminUser> create(NewUser user);

  Future<AdminUser> rename(String id, String username);

  /// Also clears any lockout and signs the user out everywhere.
  Future<AdminUser> resetPassword(String id, String password);

  Future<AdminUser> unlock(String id);

  Future<AdminUser> setAdmin(String id, {required bool isAdmin});

  Future<AdminUser> setActive(String id, {required bool isActive});

  /// Refused (409) for a user who has written comments.
  Future<void> delete(String id);
}
