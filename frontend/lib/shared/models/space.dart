/// A top-level container such as Home, Village or Software (`SpaceDto`).
/// Every work item belongs to exactly one; see the glossary's **Space**.
class Space {
  const Space({
    required this.id,
    required this.name,
    this.doneHidingDays = defaultDoneHidingDays,
  });

  factory Space.fromJson(Map<String, dynamic> json) => Space(
    id: json['id'] as String,
    name: json['name'] as String,
    // Servers that predate the setting behave as if it were the default.
    doneHidingDays: json['doneHidingDays'] as int? ?? defaultDoneHidingDays,
  );

  /// Mirror the server's defaults and limits, so the Admin page can check
  /// an entry without a round trip.
  static const int defaultDoneHidingDays = 14;
  static const int minDoneHidingDays = 1;
  static const int maxDoneHidingDays = 3650;

  final String id;
  final String name;

  /// How many days after its completion date a Done item stays on the
  /// board before it's hidden (unless the board's "Show done" is on).
  final int doneHidingDays;

  @override
  bool operator ==(Object other) =>
      other is Space &&
      other.id == id &&
      other.name == name &&
      other.doneHidingDays == doneHidingDays;

  @override
  int get hashCode => Object.hash(id, name, doneHidingDays);
}
