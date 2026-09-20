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
    final content = Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      // Lighter than the status column it sits on (surfaceContainerLow), so
      // a card actually stands out from its cell instead of blending in.
      color: Theme.of(context).colorScheme.surfaceContainerLow.lightenedBy(0.2),
      elevation: 2,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
      child: InkWell(
        borderRadius: BorderRadius.circular(10),
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
                  Text(
                    card.title,
                    softWrap: true,
                    style: const TextStyle(decoration: TextDecoration.underline),
                  ),
                  if (card.startDate != null || card.endDate != null)
                    Text(
                      _scheduleLabel(),
                      softWrap: true,
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                ],
              ),
              AssigneeAvatar(initial: assigneeInitial),
            ],
          ),
        ),
      ),
    );

    return Draggable<WorkItemCard>(
      data: card,
      feedback: Material(
        elevation: 4,
        borderRadius: BorderRadius.circular(8),
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
