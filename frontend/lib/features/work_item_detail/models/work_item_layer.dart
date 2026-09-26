class WorkItemLayer {
  const WorkItemLayer({
    required this.id,
    required this.name,
    required this.order,
  });

  factory WorkItemLayer.fromJson(Map<String, dynamic> json) => WorkItemLayer(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
  );

  final String id;
  final String name;
  final int order;
}
