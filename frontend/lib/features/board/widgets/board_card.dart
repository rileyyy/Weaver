import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/core/platform/touch_platform.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/tag_badge_row.dart';

const double _feedbackWidth = 208;

const double _handleWidth = 2;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    required this.height,
    required this.assigneeInitial,
    required this.layerColor,
    required this.onOpenDetails,
    required this.onAssignTapped,
    required this.onAddTagTapped,
  });

  final WorkItemCard card;

  /// Exact height, set by the swimlane board so its lane heights (computed
  /// from the card count) always match what renders. Content that doesn't
  /// fit is ellipsized rather than growing the card into the next lane.
  final double height;

  /// The assigned user's initial, resolved by the view model — null shows
  /// no avatar at all, whether because there's no assignee or the user
  /// directory hasn't loaded yet.
  final String? assigneeInitial;

  /// The card's layer colour, for the bar along its left edge — null (no
  /// layer, or layers not loaded) shows a neutral bar instead.
  final Color? layerColor;

  /// Called when the card is tapped, to open a dialog showing every
  /// attribute of this work item (title/description/layer/priority/
  /// assignee/comments/links/schedule). Drilling into the item's own
  /// children is reached from inside that dialog instead of from a separate
  /// tap target here.
  final VoidCallback onOpenDetails;

  /// Called when the assignee avatar itself is tapped, to open a picker
  /// that reassigns this work item directly from the board.
  final VoidCallback onAssignTapped;

  /// Called when the "+" after the tags is tapped, to add tags directly
  /// from the board.
  final VoidCallback onAddTagTapped;

  @override
  Widget build(BuildContext context) {
    final baseTitleStyle = Theme.of(context).textTheme.bodyMedium;
    final titleFontSize = (baseTitleStyle?.fontSize ?? 14) + 2;
    final titleStyle = baseTitleStyle?.copyWith(
      fontSize: titleFontSize,
      decoration: TextDecoration.underline,
    );
    // Same size as the title, but bold and not underlined — the id is a
    // label, not an editable field like the title next to it.
    final idStyle = baseTitleStyle?.copyWith(
      fontSize: titleFontSize,
      fontWeight: FontWeight.bold,
    );

    // The fixed height wraps the Card, which has no margin of its own (the
    // status column spaces cards out), so the card is exactly the height the
    // lane-height maths assumes.
    final content = SizedBox(
      height: height,
      child: Card(
        margin: EdgeInsets.zero,
        // Lighter than the status column it sits on (surfaceContainerLow) so a
        // card still stands out from its cell, but not as starkly as a full
        // 20% lightening — a little darker/closer to the column's own tone.
        color: Theme.of(
          context,
        ).colorScheme.surfaceContainerLow.lightenedBy(0.1),
        elevation: 2,
        // Square corners, not rounded — no shape override needed since
        // RoundedRectangleBorder defaults to zero radius.
        shape: const RoundedRectangleBorder(),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // A full-height color handle flush with the card's own left
            // edge; the card's fixed height gives stretch a bound.
            Container(
              width: _handleWidth,
              color: layerColor ?? Theme.of(context).colorScheme.outline,
            ),
            Expanded(
              child: InkWell(
                onTap: onOpenDetails,
                child: Padding(
                  padding: const EdgeInsets.all(8),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    // Two groups only (title block, then the assignee
                    // avatar) so spaceBetween puts all of the card's extra
                    // vertical room (when its minimum height leaves more
                    // room than the title needs) as one gap between them,
                    // pinning the avatar to the bottom.
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Flexible(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Flexible(
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  // Sized to match the id/title text next to it.
                                  Icon(Icons.emoji_events, size: titleFontSize),
                                  const SizedBox(width: 4),
                                  Text('#${card.number}', style: idStyle),
                                  const SizedBox(width: 6),
                                  // Underlined to signal the title is an
                                  // editable field, opened via onOpenDetails —
                                  // not just a label. Two lines at most; the
                                  // tooltip and the detail dialog show the rest.
                                  Expanded(
                                    child: Tooltip(
                                      message: card.title,
                                      // A tooltip also opens on long press,
                                      // which is what picks the card up on
                                      // touch platforms.
                                      triggerMode: isTouchPlatform
                                          ? TooltipTriggerMode.manual
                                          : null,
                                      child: Text(
                                        card.title,
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: titleStyle,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            if (card.startDate != null || card.endDate != null)
                              Text(
                                _scheduleLabel(),
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                          ],
                        ),
                      ),
                      Row(
                        children: [
                          AssigneeAvatar(
                            initial: assigneeInitial,
                            size: AssigneeAvatar.cardSize,
                            showPlaceholderWhenUnassigned: true,
                            onTap: onAssignTapped,
                          ),
                          const SizedBox(width: 6),
                          Expanded(
                            child: TagBadgeRow(
                              tags: card.tags,
                              onAddTag: onAddTagTapped,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );

    final feedback = Material(
      elevation: 4,
      child: SizedBox(width: _feedbackWidth, child: content),
    );
    final childWhenDragging = Opacity(opacity: 0.4, child: content);

    // A drag that starts on first contact would turn every swipe that
    // begins on a card into a move, leaving almost nothing to scroll the
    // board with on a phone. Touch platforms hold to pick a card up instead.
    if (isTouchPlatform) {
      return LongPressDraggable<WorkItemCard>(
        data: card,
        feedback: feedback,
        childWhenDragging: childWhenDragging,
        child: content,
      );
    }
    return Draggable<WorkItemCard>(
      data: card,
      feedback: feedback,
      childWhenDragging: childWhenDragging,
      child: content,
    );
  }

  String _scheduleLabel() {
    final start = card.startDate;
    final end = card.endDate;
    if (start != null && end != null) {
      return '${formatDate(start)} → ${formatDate(end)}';
    }
    if (start != null) return 'From ${formatDate(start)}';
    return 'Until ${formatDate(end!)}';
  }
}
