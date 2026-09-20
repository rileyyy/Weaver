import 'package:flutter/material.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

const double _feedbackWidth = 208;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    required this.assigneeInitial,
    required this.onOpenDetails,
  });

  final WorkItemCard card;

  /// The assigned user's initial, resolved by the view model — null shows
  /// no avatar at all, whether because there's no assignee or the user
  /// directory hasn't loaded yet.
  final String? assigneeInitial;

  /// Called when the card is tapped, to open a dialog showing every
  /// attribute of this work item (title/description/layer/priority/
  /// assignee/comments/links/schedule). Drilling into the item's own
  /// children is reached from inside that dialog instead of from a separate
  /// tap target here.
  final VoidCallback onOpenDetails;

  @override
  Widget build(BuildContext context) {
    final baseTitleStyle = Theme.of(context).textTheme.bodyMedium;
    final titleStyle = baseTitleStyle?.copyWith(
      fontSize: (baseTitleStyle.fontSize ?? 14) + 2,
      decoration: TextDecoration.underline,
    );

    final content = Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      // Lighter than the status column it sits on (surfaceContainerLow) so a
      // card still stands out from its cell, but not as starkly as a full
      // 20% lightening — a little darker/closer to the column's own tone.
      color: Theme.of(context).colorScheme.surfaceContainerLow.lightenedBy(0.1),
      elevation: 2,
      // Square corners, not rounded — no shape override needed since
      // RoundedRectangleBorder defaults to zero radius.
      shape: const RoundedRectangleBorder(),
      child: InkWell(
        onTap: onOpenDetails,
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            // Two groups only (title block, then the assignee avatar) so
            // spaceBetween puts all of the card's extra vertical room (when
            // its minimum height leaves more room than the title needs) as
            // one gap between them, pinning the avatar to the bottom.
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  // Underlined to signal the title is an editable field,
                  // opened via onOpenDetails — not just a label. Soft-wraps
                  // rather than truncating: cards are narrow (two per column
                  // row), so a longer title needs the extra lines.
                  Text(card.title, softWrap: true, style: titleStyle),
                  if (card.startDate != null || card.endDate != null)
                    Text(
                      _scheduleLabel(),
                      softWrap: true,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                ],
              ),
              // Twice the default size — big enough to actually stand out
              // at a glance, per explicit feedback on the original 24px one.
              AssigneeAvatar(
                initial: assigneeInitial,
                size: AssigneeAvatar.defaultSize * 2,
              ),
            ],
          ),
        ),
      ),
    );

    return Draggable<WorkItemCard>(
      data: card,
      feedback: Material(
        elevation: 4,
        child: SizedBox(width: _feedbackWidth, child: content),
      ),
      childWhenDragging: Opacity(opacity: 0.4, child: content),
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
