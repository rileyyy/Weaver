import 'dart:ui' show Color;

import 'package:weaver/shared/models/hex_color.dart';

/// What kind of thing a work item is (e.g. Project / Goal / Task). Mirrors
/// the backend's `WorkItemType`.
class WorkItemType {
  const WorkItemType({
    required this.id,
    required this.name,
    required this.order,
    this.color,
  });

  factory WorkItemType.fromJson(Map<String, dynamic> json) => WorkItemType(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
    color: parseHexColor(json['color'] as String?),
  );

  final String id;
  final String name;
  final int order;

  /// Marks this type's items on the board. Nullable on the same terms as
  /// `WorkItemStatus.color`.
  final Color? color;
}
