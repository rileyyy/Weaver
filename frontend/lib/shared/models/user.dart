import 'package:weaver/shared/models/user_kind.dart';

class User {
  const User({required this.id, required this.username, required this.kind});

  factory User.fromJson(Map<String, dynamic> json) => User(
    id: json['id'] as String,
    username: json['username'] as String,
    kind: userKindFromWire(json['kind'] as String),
  );

  final String id;
  final String username;
  final UserKind kind;
}
