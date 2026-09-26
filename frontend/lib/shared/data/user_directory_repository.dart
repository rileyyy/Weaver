import 'package:weaver/shared/models/user.dart';

/// Every registered user, for assignee pickers and name lookups.
abstract class UserDirectoryRepository {
  Future<List<User>> loadUsers();
}
