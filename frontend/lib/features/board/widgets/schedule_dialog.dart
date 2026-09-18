import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

class ScheduleResult {
  const ScheduleResult(this.startDate, this.endDate);

  final DateTime? startDate;
  final DateTime? endDate;
}

/// Prompts for a start/end date, pre-filled from [initialStart]/[initialEnd].
/// Returns null if the dialog was cancelled.
Future<ScheduleResult?> showScheduleDialog(
  BuildContext context, {
  DateTime? initialStart,
  DateTime? initialEnd,
}) {
  return showDialog<ScheduleResult>(
    context: context,
    builder: (context) => _ScheduleDialog(initialStart: initialStart, initialEnd: initialEnd),
  );
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
            ScheduleResult(_start, _end),
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
