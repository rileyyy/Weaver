import 'package:flutter/material.dart';

/// Blends a color toward white by a given amount, used to give the
/// swimlane board a background visually distinct (lighter) from the
/// filter/header section above it, in both light and dark theme.
extension ColorShade on Color {
  Color lightenedBy(double amount) => Color.lerp(this, Colors.white, amount)!;
}

abstract final class AppTheme {
  static const Color _seedColor = Colors.indigo;

  static ThemeData get light => ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: _seedColor),
        useMaterial3: true,
      );

  static ThemeData get dark => ThemeData(
        colorScheme: ColorScheme.fromSeed(
          seedColor: _seedColor,
          brightness: Brightness.dark,
        ),
        useMaterial3: true,
      );
}
