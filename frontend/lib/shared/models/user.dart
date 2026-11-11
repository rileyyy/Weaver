import 'package:weaver/shared/models/user_kind.dart';

class User {
  const User({
    required this.id,
    required this.username,
    required this.kind,
    this.isAdmin = false,
    this.handle,
  });

  factory User.fromJson(Map<String, dynamic> json) => User(
    id: json['id'] as String,
    username: json['username'] as String,
    kind: UserKind.fromWire(json['kind'] as String),
    isAdmin: json['isAdmin'] as bool,
    handle: json['handle'] as String?,
  );

  final String id;
  final String username;
  final UserKind kind;

  /// May open the Admin page. The server checks this again on every admin
  /// request, so a stale value only affects whether the gear is shown.
  final bool isAdmin;

  /// The `@` name a comment mentions this account by; only agents have one
  /// so far.
  final String? handle;
}
