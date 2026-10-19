import 'dart:ui' show Color;

import 'package:weaver/shared/models/hex_color.dart';

/// A board column. Mirrors the backend's `Status` shape — see
/// `backend/src/Weaver.Domain/Status.cs`.
class WorkItemStatus {
  const WorkItemStatus({
    required this.id,
    required this.name,
    required this.order,
    this.color,
  });

  factory WorkItemStatus.fromJson(Map<String, dynamic> json) => WorkItemStatus(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
    color: parseHexColor(json['color'] as String?),
  );

  final String id;
  final String name;
  final int order;

  /// The status's configured display color, parsed from the backend's
  /// `#RRGGBB` hex string. Nullable so fixtures/tests that don't care about
  /// color don't need to supply one — a real API response always has it.
  final Color? color;
}
