import 'package:flutter/material.dart';

const double _horizontalPadding = 6;
const double _verticalPadding = 2;
const double _borderRadius = 10;
const double _fontSize = 11;

/// A small rounded-rect chip showing one tag's label. [isOverflow] renders
/// the "+N" variant [TagBadgeRow] appends when not every tag fits — visually
/// muted to signal it isn't a real tag (never counted or matched by
/// search/filter, never removable).
class TagBadge extends StatelessWidget {
  const TagBadge({super.key, required this.label, this.isOverflow = false});

  final String label;
  final bool isOverflow;

  static TextStyle _textStyle(BuildContext context) =>
      (Theme.of(context).textTheme.labelSmall ?? const TextStyle()).copyWith(
        fontSize: _fontSize,
      );

  /// The exact rendered width of a badge showing [label] — used by
  /// [TagBadgeRow]'s fitting logic so its measurement matches what this
  /// widget actually paints.
  static double widthFor(BuildContext context, String label) {
    final painter = TextPainter(
      text: TextSpan(text: label, style: _textStyle(context)),
      textDirection: TextDirection.ltr,
      textScaler: MediaQuery.textScalerOf(context),
    )..layout();
    return painter.width + _horizontalPadding * 2;
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: _horizontalPadding,
        vertical: _verticalPadding,
      ),
      decoration: BoxDecoration(
        color: isOverflow
            ? colorScheme.surfaceContainerHighest
            : colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(_borderRadius),
      ),
      child: Text(
        label,
        style: _textStyle(context).copyWith(
          color: isOverflow
              ? colorScheme.onSurfaceVariant
              : colorScheme.onSecondaryContainer,
        ),
      ),
    );
  }
}
