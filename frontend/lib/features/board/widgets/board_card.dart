import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

const double _feedbackWidth = 208;

class BoardCard extends StatelessWidget {
  const BoardCard({
    super.key,
    required this.card,
    this.onOpen,
    required this.onReschedule,
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
    final result = await showDialog<_ScheduleResult>(
      context: context,
      builder: (context) => _ScheduleDialog(
        initialStart: card.startDate,
        initialEnd: card.endDate,
      ),
    );
    if (result == null) return;
    await onReschedule(card, result.startDate, result.endDate);
  }
}

class _ScheduleResult {
  const _ScheduleResult(this.startDate, this.endDate);

  final DateTime? startDate;
  final DateTime? endDate;
}

class _ScheduleDialog extends StatefulWidget {
  const _ScheduleDialog({this.initialStart, this.initialEnd});

  final DateTime? initialStart;
  final DateTime? initialEnd;

  @override
  State<_ScheduleDialog> createState() => _ScheduleDialogState();
}

class _ScheduleDialogState extends State<_ScheduleDialog> {
  late DateTime? _start = widget.initialStart;
  late DateTime? _end = widget.initialEnd;

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Schedule'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          _DateRow(
            label: 'Start',
            value: _start,
            onPick: () => unawaited(_pickStart()),
            onClear: () => setState(() => _start = null),
          ),
          _DateRow(
            label: 'End',
            value: _end,
            onPick: () => unawaited(_pickEnd()),
            onClear: () => setState(() => _end = null),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () => Navigator.of(context).pop(
            _ScheduleResult(_start, _end),
          ),
          child: const Text('Save'),
        ),
      ],
    );
  }

  Future<void> _pickStart() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _start ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) setState(() => _start = picked);
  }

  Future<void> _pickEnd() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _end ?? _start ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) setState(() => _end = picked);
  }
}

class _DateRow extends StatelessWidget {
  const _DateRow({
    required this.label,
    required this.value,
    required this.onPick,
    required this.onClear,
  });

  final String label;
  final DateTime? value;
  final VoidCallback onPick;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(width: 48, child: Text(label)),
        Expanded(
          child: TextButton(
            onPressed: onPick,
            child: Text(value == null ? 'Not set' : formatDate(value!)),
          ),
        ),
        if (value != null)
          IconButton(icon: const Icon(Icons.clear, size: 16), onPressed: onClear),
      ],
    );
  }
}
