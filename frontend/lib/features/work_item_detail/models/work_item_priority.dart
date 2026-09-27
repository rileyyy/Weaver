/// One of the configurable priorities (seeded Low / Medium / High / Urgent),
/// loaded from the backend like [WorkItemLayer]s. [order] runs from least to
/// most urgent.
class WorkItemPriority {
  const WorkItemPriority({
    required this.id,
    required this.name,
    required this.order,
  });

  factory WorkItemPriority.fromJson(Map<String, dynamic> json) =>
      WorkItemPriority(
        id: json['id'] as String,
        name: json['name'] as String,
        order: json['order'] as int,
      );

  final String id;
  final String name;
  final int order;
}
