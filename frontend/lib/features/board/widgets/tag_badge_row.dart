import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

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
      final reserve = remainingAfterThis > 0
          ? spacing + TagBadge.widthFor(context, '+$remainingAfterThis')
          : 0.0;
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
