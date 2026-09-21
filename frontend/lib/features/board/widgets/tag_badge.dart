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
      (Theme.of(context).textTheme.labelSmall ?? const TextStyle()).copyWith(fontSize: _fontSize);

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
      padding: const EdgeInsets.symmetric(horizontal: _horizontalPadding, vertical: _verticalPadding),
      decoration: BoxDecoration(
        color: isOverflow ? colorScheme.surfaceContainerHighest : colorScheme.secondaryContainer,
        borderRadius: BorderRadius.circular(_borderRadius),
      ),
      child: Text(
        label,
        style: _textStyle(context).copyWith(
          color: isOverflow ? colorScheme.onSurfaceVariant : colorScheme.onSecondaryContainer,
        ),
      ),
    );
  }
}

/// A single-line row of [TagBadge]s that fits as many of [tags] as possible
/// within whatever width its parent gives it, appending a "+N" overflow
/// badge for the rest when they don't all fit. Renders nothing for an empty
/// tag list.
class TagBadgeRow extends StatelessWidget {
  const TagBadgeRow({super.key, required this.tags, this.spacing = 4});

  final List<String> tags;
  final double spacing;

  @override
  Widget build(BuildContext context) {
    if (tags.isEmpty) return const SizedBox.shrink();

    return LayoutBuilder(
      builder: (context, constraints) {
        final visibleCount = _visibleCountFor(context, constraints.maxWidth);
        final overflowCount = tags.length - visibleCount;

        return Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < visibleCount; i++) ...[
              if (i > 0) SizedBox(width: spacing),
              TagBadge(label: tags[i]),
            ],
            if (overflowCount > 0) ...[
              if (visibleCount > 0) SizedBox(width: spacing),
              TagBadge(label: '+$overflowCount', isOverflow: true),
            ],
          ],
        );
      },
    );
  }

  /// Greedily fits as many tags as possible within [maxWidth], always
  /// reserving room for a trailing "+N" badge sized to whatever count would
  /// remain hidden at each step, so the eventual overflow badge's own width
  /// is never underestimated.
  int _visibleCountFor(BuildContext context, double maxWidth) {
    if (maxWidth <= 0) return 0;

    var used = 0.0;
    var visible = 0;
    for (var i = 0; i < tags.length; i++) {
      final tagWidth = TagBadge.widthFor(context, tags[i]);
      final remainingAfterThis = tags.length - (i + 1);
      final reserve =
          remainingAfterThis > 0 ? spacing + TagBadge.widthFor(context, '+$remainingAfterThis') : 0.0;
      final gap = i > 0 ? spacing : 0.0;

      if (used + gap + tagWidth + reserve <= maxWidth) {
        used += gap + tagWidth;
        visible++;
      } else {
        break;
      }
    }
    return visible;
  }
}
