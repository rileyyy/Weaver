import 'package:weaver/features/admin/models/status_category.dart';

/// What the status dialog collects, for creating or editing a status.
class StatusDetails {
  const StatusDetails({
    required this.name,
    required this.category,
    required this.color,
  });

  final String name;
  final StatusCategory category;

  /// `#RRGGBB`.
  final String color;

  Map<String, dynamic> toJson() => {
    'name': name,
    'category': category.wire,
    'color': color,
  };
}
