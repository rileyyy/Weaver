/// Mirrors the backend's `UserKind`.
enum UserKind {
  human(wire: 'Human'),
  agent(wire: 'Agent');

  const UserKind({required this.wire});

  final String wire;

  /// Throws a [FormatException] for an unknown value, which the API client
  /// reports as an unexpected response instead of silently treating the
  /// user as human.
  static UserKind fromWire(String value) => values.firstWhere(
    (kind) => kind.wire == value,
    orElse: () => throw FormatException('Unknown user kind', value),
  );
}
