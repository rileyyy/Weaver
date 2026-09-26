import 'package:weaver/shared/models/user.dart';

/// The user list plus an id lookup, so every card's avatar resolves in
/// constant time instead of scanning the whole list.
class UserDirectory {
  UserDirectory(this.users) : _byId = {for (final user in users) user.id: user};

  static final UserDirectory empty = UserDirectory(const []);

  final List<User> users;
  final Map<String, User> _byId;

  /// Null when [userId] is null or not (yet) known.
  String? usernameFor(String? userId) =>
      userId == null ? null : _byId[userId]?.username;

  /// The uppercased first letter of the username, for a small avatar.
  String? initialFor(String? userId) {
    final username = usernameFor(userId);
    return username == null || username.isEmpty
        ? null
        : username[0].toUpperCase();
  }
}
