enum UserKind { human, agent }

UserKind userKindFromWire(String value) => switch (value) {
  'Agent' => UserKind.agent,
  _ => UserKind.human,
};

class AuthUser {
  const AuthUser({
    required this.id,
    required this.username,
    required this.kind,
  });

  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
    id: json['id'] as String,
    username: json['username'] as String,
    kind: userKindFromWire(json['kind'] as String),
  );

  final String id;
  final String username;
  final UserKind kind;
}
