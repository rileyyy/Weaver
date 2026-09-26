import 'package:flutter/material.dart';

/// Blends a color toward white by a given amount, used to give the
/// swimlane board a background visually distinct (lighter) from the
/// filter/header section above it, in both light and dark theme.
extension ColorShade on Color {
  Color lightenedBy(double amount) => Color.lerp(this, Colors.white, amount)!;

  /// Blends a color toward black by a given amount — the swim-lane grid's
  /// background is deliberately dark (as dark as a status color swatch),
  /// distinct from the lighter tone the rest of the header/filter chrome
  /// uses.
  Color darkenedBy(double amount) => Color.lerp(this, Colors.black, amount)!;
}

abstract final class AppTheme {
  static const Color _seedColor = Colors.indigo;

  /// Scales every text style slightly above Material's defaults, applied
  /// uniformly via [TextTheme.apply] rather than bumping individual style
  /// sizes, so relative proportions (title vs. body vs. caption) stay
  /// consistent everywhere text is rendered.
  static const double _fontSizeFactor = 1.1;

  static ThemeData get light => _withScaledFonts(
    ThemeData(
      colorScheme: ColorScheme.fromSeed(seedColor: _seedColor),
      useMaterial3: true,
    ),
  );

  static ThemeData get dark => _withScaledFonts(
    ThemeData(
      colorScheme: ColorScheme.fromSeed(
        seedColor: _seedColor,
        brightness: Brightness.dark,
      ),
      useMaterial3: true,
    ),
  );

  static ThemeData _withScaledFonts(ThemeData theme) =>
      theme.copyWith(textTheme: _scaled(theme.textTheme));

  // Scales each style by hand rather than via TextTheme.apply(fontSizeFactor:
  // ...) — some roles in Material 3's default TextTheme carry a null
  // fontSize, and TextStyle.apply asserts against scaling a null fontSize.
  // Passing those through unscaled avoids the crash without losing any size
  // that was actually set.
  static TextTheme _scaled(TextTheme base) => TextTheme(
    displayLarge: _scaleStyle(base.displayLarge),
    displayMedium: _scaleStyle(base.displayMedium),
    displaySmall: _scaleStyle(base.displaySmall),
    headlineLarge: _scaleStyle(base.headlineLarge),
    headlineMedium: _scaleStyle(base.headlineMedium),
    headlineSmall: _scaleStyle(base.headlineSmall),
    titleLarge: _scaleStyle(base.titleLarge),
    titleMedium: _scaleStyle(base.titleMedium),
    titleSmall: _scaleStyle(base.titleSmall),
    bodyLarge: _scaleStyle(base.bodyLarge),
    bodyMedium: _scaleStyle(base.bodyMedium),
    bodySmall: _scaleStyle(base.bodySmall),
    labelLarge: _scaleStyle(base.labelLarge),
    labelMedium: _scaleStyle(base.labelMedium),
    labelSmall: _scaleStyle(base.labelSmall),
  );

  static TextStyle? _scaleStyle(TextStyle? style) {
    final fontSize = style?.fontSize;
    if (style == null || fontSize == null) return style;
    return style.copyWith(fontSize: fontSize * _fontSizeFactor);
  }
}
