/// Mirrors the backend's `WorkItemPriority`. [wire] is the API contract and
/// [label] the display text; keeping them separate lets the UI wording change
/// without breaking requests.
enum WorkItemPriority {
  low(wire: 'Low', label: 'Low'),
  medium(wire: 'Medium', label: 'Medium'),
  high(wire: 'High', label: 'High'),
  urgent(wire: 'Urgent', label: 'Urgent');

  const WorkItemPriority({required this.wire, required this.label});

  final String wire;
  final String label;

  /// Throws a [FormatException] for an unknown value, which the API client
  /// reports as an unexpected response instead of silently showing Medium.
  static WorkItemPriority fromWire(String value) => values.firstWhere(
    (priority) => priority.wire == value,
    orElse: () => throw FormatException('Unknown work item priority', value),
  );
}
