/// Mirrors the backend's `StatusCategory`: what a status means, whatever it
/// is called.
enum StatusCategory {
  toDo(wire: 'ToDo', label: 'To do'),
  doing(wire: 'Doing', label: 'In progress'),
  done(wire: 'Done', label: 'Done');

  const StatusCategory({required this.wire, required this.label});

  final String wire;
  final String label;

  /// Throws a [FormatException] for an unknown value, which the API client
  /// reports as an unexpected response.
  static StatusCategory fromWire(String value) => values.firstWhere(
    (category) => category.wire == value,
    orElse: () => throw FormatException('Unknown status category', value),
  );
}
