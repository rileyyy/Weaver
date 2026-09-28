/// A priority or layer: just a name in an order.
class LookupValue {
  const LookupValue({
    required this.id,
    required this.name,
    required this.order,
  });

  factory LookupValue.fromJson(Map<String, dynamic> json) => LookupValue(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
  );

  final String id;
  final String name;
  final int order;
}
