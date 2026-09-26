import 'dart:ui' show Color;

/// A board column. Mirrors the backend's `Status` shape — see
/// `backend/src/Weaver.Domain/Status.cs`.
class BoardStatus {
  const BoardStatus({
    required this.id,
    required this.name,
    required this.order,
    this.color,
  });

  factory BoardStatus.fromJson(Map<String, dynamic> json) => BoardStatus(
    id: json['id'] as String,
    name: json['name'] as String,
    order: json['order'] as int,
    color: parseStatusColor(json['color'] as String),
  );

  final String id;
  final String name;
  final int order;

  /// The status's configured display color, parsed from the backend's
  /// `#RRGGBB` hex string. Nullable so fixtures/tests that don't care about
  /// color don't need to supply one — a real API response always has it.
  final Color? color;
}

/// Parses the backend's `#RRGGBB` hex string (see `Status.Color`) into a
/// [Color]. Shared by every repository that maps a status from JSON.
Color parseStatusColor(String hex) =>
    Color(int.parse(hex.replaceFirst('#', 'FF'), radix: 16));
