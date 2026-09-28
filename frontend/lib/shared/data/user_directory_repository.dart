import 'package:weaver/shared/models/user.dart';

/// Every active user, for assignee pickers and name lookups.
abstract class UserDirectoryRepository {
  Future<List<User>> loadUsers();

  /// Drops the cached list, so the next [loadUsers] fetches it again.
  /// Called after an admin changes an account.
  void invalidate();
}
