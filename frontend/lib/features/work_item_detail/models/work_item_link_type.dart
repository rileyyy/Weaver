/// Mirrors the backend's `WorkItemLinkType`, always seen from the item it
/// is listed on: a "blocks" link is stored once and reads as [blockedBy]
/// from the other item.
enum WorkItemLinkType {
  related(wire: 'Related', label: 'Related', phrase: 'related to'),
  blocks(wire: 'Blocks', label: 'Blocks', phrase: 'blocks'),
  blockedBy(wire: 'BlockedBy', label: 'Blocked by', phrase: 'blocked by'),
  duplicates(wire: 'Duplicates', label: 'Duplicates', phrase: 'duplicates'),
  duplicatedBy(
    wire: 'DuplicatedBy',
    label: 'Duplicated by',
    phrase: 'duplicated by',
  );

  const WorkItemLinkType({
    required this.wire,
    required this.label,
    required this.phrase,
  });

  final String wire;
  final String label;

  /// The type mid-sentence, before the other item: "blocked by #4 Paint".
  final String phrase;

  /// Throws a [FormatException] for an unknown value, which the API client
  /// reports as an unexpected response.
  static WorkItemLinkType fromWire(String value) => values.firstWhere(
    (type) => type.wire == value,
    orElse: () => throw FormatException('Unknown link type', value),
  );

  /// Null for an unknown value, for history rows that must still render.
  static WorkItemLinkType? tryFromWire(String value) {
    for (final type in values) {
      if (type.wire == value) return type;
    }
    return null;
  }
}
