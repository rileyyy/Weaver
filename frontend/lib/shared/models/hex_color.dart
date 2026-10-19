import 'dart:ui' show Color;

final _hexColor = RegExp(r'^#?([0-9a-fA-F]{6})$');

/// Parses the backend's `#RRGGBB` hex string (see `HexColor` on the
/// backend) into an opaque [Color], tolerating a missing `#`. Returns null
/// for anything else, so one malformed status or layer colour can't fail a
/// whole load; it then renders with a default colour.
Color? parseHexColor(String? hex) {
  final match = _hexColor.firstMatch(hex?.trim() ?? '');
  if (match == null) return null;
  return Color(0xFF000000 | int.parse(match.group(1)!, radix: 16));
}
