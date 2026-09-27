import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/add_tag_button.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

/// A single-line row of [TagBadge]s that fits as many of [tags] as possible
/// within whatever width its parent gives it, appending a "+N" overflow
/// badge for the rest when they don't all fit. Given [onAddTag], an
/// [AddTagButton] always ends the row (room for it is reserved first) and
/// calls it when tapped. Renders nothing for an empty tag list without [onAddTag].
class TagBadgeRow extends StatelessWidget {
  const TagBadgeRow({
    super.key,
    required this.tags,
    this.spacing = 4,
    this.onAddTag,
  });

  final List<String> tags;
  final double spacing;
  final VoidCallback? onAddTag;

  @override
  Widget build(BuildContext context) {
    final onAddTag = this.onAddTag;
    if (tags.isEmpty && onAddTag == null) return const SizedBox.shrink();

    return LayoutBuilder(
      builder: (context, constraints) {
        final showAdd =
            onAddTag != null && AddTagButton.size <= constraints.maxWidth;
        final tagsWidth = showAdd
            ? constraints.maxWidth - AddTagButton.size - spacing
            : constraints.maxWidth;
        final visibleCount = _visibleCountFor(context, tagsWidth);
        final overflowCount = tags.length - visibleCount;
        // With no tag shown, the "+N" badge alone may not fit; when some
        // are shown, _visibleCountFor already left room for it.
        final showOverflow =
            overflowCount > 0 &&
            (visibleCount > 0 ||
                TagBadge.widthFor(context, '+$overflowCount') <= tagsWidth);
        final showsAnyTagBadge = visibleCount > 0 || showOverflow;

        return Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < visibleCount; i++) ...[
              if (i > 0) SizedBox(width: spacing),
              TagBadge(label: tags[i]),
            ],
            if (showOverflow) ...[
              if (visibleCount > 0) SizedBox(width: spacing),
              TagBadge(label: '+$overflowCount', isOverflow: true),
            ],
            if (showAdd) ...[
              if (showsAnyTagBadge) SizedBox(width: spacing),
              AddTagButton(onPressed: onAddTag),
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
