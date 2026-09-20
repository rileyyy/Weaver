import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/schedule_dialog.dart';

const double _feedbackWidth = 208;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    required this.assigneeInitial,
    required this.onReschedule,
    required this.onOpenDetails,
  });

  final WorkItemCard card;

  /// The assigned user's initial, resolved by the view model — null shows
  /// no avatar at all, whether because there's no assignee or the user
  /// directory hasn't loaded yet.
  final String? assigneeInitial;

  /// Called with the new start/end after the schedule dialog is saved.
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onReschedule;

  /// Called when the card is tapped, to open a dialog showing every
  /// attribute of this work item (title/description/layer/priority/
  /// assignee/comments/links). Drilling into the item's own children is
  /// reached from inside that dialog instead of from a separate tap target
  /// here.
  final VoidCallback onOpenDetails;

  @override
  Widget build(BuildContext context) {
    final content = Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
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
            children: [
              // Underlined to signal the title is an editable field, opened
              // via onOpenDetails — not just a label. Soft-wraps rather than
              // truncating: cards are narrow (two per column row), so a
              // longer title needs the extra lines.
              Text(
                card.title,
                softWrap: true,
                style: const TextStyle(decoration: TextDecoration.underline),
              ),
              if (card.startDate != null || card.endDate != null)
                Text(
                  _scheduleLabel(),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              const SizedBox(height: 4),
              // Assignee avatar and the schedule button live on their own
              // row, below the title — kept off the title's row so a
              // wrapped title doesn't fight them for horizontal space in a
              // narrow card.
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  AssigneeAvatar(initial: assigneeInitial),
                  IconButton(
                    icon: const Icon(Icons.calendar_today, size: 16),
                    tooltip: 'Set schedule',
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
                    visualDensity: VisualDensity.compact,
                    onPressed: () => unawaited(_editSchedule(context)),
                  ),
                ],
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

  Future<void> _editSchedule(BuildContext context) async {
    final result = await showScheduleDialog(
      context,
      initialStart: card.startDate,
      initialEnd: card.endDate,
    );
    if (result == null) return;
    await onReschedule(card, result.startDate, result.endDate);
  }
}
