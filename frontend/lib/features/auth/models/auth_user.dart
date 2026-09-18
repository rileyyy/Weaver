enum UserKind { human, agent }

UserKind userKindFromWire(String value) => switch (value) {
      'Agent' => UserKind.agent,
      _ => UserKind.human,
    };

class AuthUser {
  const AuthUser({required this.id, required this.username, required this.kind});

  final String id;
  final String username;
  final UserKind kind;
}
