/// A top-level container such as Home, Village or Software (`SpaceDto`).
/// Every work item belongs to exactly one; see the glossary's **Space**.
class Space {
  const Space({required this.id, required this.name});

  factory Space.fromJson(Map<String, dynamic> json) =>
      Space(id: json['id'] as String, name: json['name'] as String);

  final String id;
  final String name;

  @override
  bool operator ==(Object other) =>
      other is Space && other.id == id && other.name == name;

  @override
  int get hashCode => Object.hash(id, name);
}
