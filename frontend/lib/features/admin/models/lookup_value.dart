/// A priority or layer: a name in an order, and for layers a colour.
class LookupValue {
  const LookupValue({
    required this.id,
    required this.name,
    required this.order,
    this.color,
  });

  factory LookupValue.fromJson(Map<String, dynamic> json) => LookupValue(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
    color: json['color'] as String?,
  );

  final String id;
  final String name;
  final int order;

  /// The `#RRGGBB` colour, for kinds where `LookupKind.hasColor`; null for
  /// the others.
  final String? color;
}
