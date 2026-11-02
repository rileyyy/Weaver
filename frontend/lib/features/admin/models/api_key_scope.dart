/// Mirrors the backend's `ApiKeyScope`. Neither scope can change admin data.
enum ApiKeyScope {
  readOnly(
    wire: 'ReadOnly',
    label: 'Read-only',
    explanation: 'Reads everything and writes nothing.',
  ),
  readWrite(
    wire: 'ReadWrite',
    label: 'Read-write',
    explanation: 'Does what a non-admin person can.',
  );

  const ApiKeyScope({
    required this.wire,
    required this.label,
    required this.explanation,
  });

  final String wire;
  final String label;
  final String explanation;

  /// Throws a [FormatException] for an unknown value, which the API client
  /// reports as an unexpected response rather than guessing a scope.
  static ApiKeyScope fromWire(String value) => values.firstWhere(
    (scope) => scope.wire == value,
    orElse: () => throw FormatException('Unknown API key scope', value),
  );
}
