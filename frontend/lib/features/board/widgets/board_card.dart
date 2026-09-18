import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/schedule_dialog.dart';

const double _feedbackWidth = 208;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    this.onOpen,
    required this.onReschedule,
    required this.onOpenDetails,
  });

  final WorkItemCard card;

  /// Called when the card is tapped, to drill into its own children.
  final VoidCallback? onOpen;

  /// Called with the new start/end after the schedule dialog is saved.
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onReschedule;

  /// Called when the details icon is pressed, to open the full work-item
  /// detail screen (title/description/layer/priority/assignee) — separate
  /// from [onOpen], which drills into the card's children instead.
  final VoidCallback onOpenDetails;

  @override
  Widget build(BuildContext context) {
    final content = Card(
      margin: const EdgeInsets.symmetric(vertical: 4),
      child: InkWell(
        onTap: onOpen,
        child: Padding(
          padding: const EdgeInsets.all(8),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(card.title),
                    if (card.startDate != null || card.endDate != null)
                      Text(
                        _scheduleLabel(),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                  ],
                ),
              ),
              IconButton(
                icon: const Icon(Icons.info_outline, size: 16),
                tooltip: 'View details',
                onPressed: onOpenDetails,
              ),
              IconButton(
                icon: const Icon(Icons.calendar_today, size: 16),
                tooltip: 'Set schedule',
                onPressed: () => unawaited(_editSchedule(context)),
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
