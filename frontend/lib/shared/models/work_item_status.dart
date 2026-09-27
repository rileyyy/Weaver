import 'dart:ui' show Color;

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
    color: parseStatusColor(json['color'] as String?),
  );

  final String id;
  final String name;
  final int order;

  /// The status's configured display color, parsed from the backend's
  /// `#RRGGBB` hex string. Nullable so fixtures/tests that don't care about
  /// color don't need to supply one — a real API response always has it.
  final Color? color;
}

final _hexColor = RegExp(r'^#?([0-9a-fA-F]{6})$');

/// Parses the backend's `#RRGGBB` hex string (see `Status.Color`) into an
/// opaque [Color], tolerating a missing `#`. Returns null for anything else,
/// so one malformed status colour can't fail the whole board load; the
/// status then renders with the default colour.
Color? parseStatusColor(String? hex) {
  final match = _hexColor.firstMatch(hex?.trim() ?? '');
  if (match == null) return null;
  return Color(0xFF000000 | int.parse(match.group(1)!, radix: 16));
}
