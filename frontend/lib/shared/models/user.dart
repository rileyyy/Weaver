import 'package:weaver/shared/models/user_kind.dart';

class User {
  const User({
    required this.id,
    required this.username,
    required this.kind,
    this.isAdmin = false,
  });

  factory User.fromJson(Map<String, dynamic> json) => User(
    id: json['id'] as String,
    username: json['username'] as String,
    kind: UserKind.fromWire(json['kind'] as String),
    isAdmin: json['isAdmin'] as bool,
  );

  final String id;
  final String username;
  final UserKind kind;

  /// May open the Admin page. The server checks this again on every admin
  /// request, so a stale value only affects whether the gear is shown.
  final bool isAdmin;
}
