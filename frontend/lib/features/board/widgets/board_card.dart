import 'package:flutter/material.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

const double _feedbackWidth = 208;

/// Placeholder for a future status/priority-driven color — static for now,
/// per explicit direction, rather than derived from the card's own data.
const Color _handleColor = Colors.green;
const double _handleWidth = 2;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    required this.assigneeInitial,
    required this.onOpenDetails,
    required this.onAssignTapped,
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

  /// Called when the assignee avatar itself is tapped, to open a picker
  /// that reassigns this work item directly from the board.
  final VoidCallback onAssignTapped;

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
      // IntrinsicHeight, not just Row's own CrossAxisAlignment.stretch: the
      // card's own height constraint is a bare minHeight with an unbounded
      // max (see the ConstrainedBox in _StatusColumn, which lets a card grow
      // for a long title) — stretch alone needs a bounded cross axis to
      // stretch into, which an unbounded max doesn't give it. IntrinsicHeight
      // measures the row's natural height first and feeds that back in as a
      // tight constraint, which stretch can then use safely.
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // A full-height color handle flush with the card's own left
            // edge — stretch (above) is what makes it span the card's
            // actual height rather than needing one of its own.
            Container(width: _handleWidth, color: _handleColor),
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
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              // Sized to match the id/title text next to it.
                              Icon(Icons.emoji_events, size: titleFontSize),
                              const SizedBox(width: 4),
                              Text('#${card.number}', style: idStyle),
                              const SizedBox(width: 6),
                              // Underlined to signal the title is an
                              // editable field, opened via onOpenDetails —
                              // not just a label. Soft-wraps rather than
                              // truncating: cards are narrow (two per
                              // column row), so a longer title needs the
                              // extra lines.
                              Expanded(
                                child: Text(
                                  card.title,
                                  softWrap: true,
                                  style: titleStyle,
                                ),
                              ),
                            ],
                          ),
                          if (card.startDate != null || card.endDate != null)
                            Text(
                              _scheduleLabel(),
                              softWrap: true,
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                        ],
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
                          Expanded(child: TagBadgeRow(tags: card.tags)),
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
