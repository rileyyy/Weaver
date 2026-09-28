import 'package:weaver/features/admin/models/status_category.dart';

/// A status as the Admin page edits it: unlike the shared `WorkItemStatus`,
/// it keeps the category and the colour as the `#RRGGBB` text the server
/// stores.
class AdminStatus {
  const AdminStatus({
    required this.id,
    required this.name,
    required this.order,
    required this.category,
    required this.color,
  });

  factory AdminStatus.fromJson(Map<String, dynamic> json) => AdminStatus(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
    category: StatusCategory.fromWire(json['category'] as String),
    color: json['color'] as String,
  );

  final String id;
  final String name;
  final int order;
  final StatusCategory category;
  final String color;
}
