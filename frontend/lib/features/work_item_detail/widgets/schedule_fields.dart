import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/date_field.dart';

/// Start and end date fields. Each picker is bounded by the other date, so
/// an inverted range can't be picked.
class ScheduleFields extends StatelessWidget {
  const ScheduleFields({
    super.key,
    required this.startDate,
    required this.endDate,
    required this.onChanged,
  });

  final DateTime? startDate;
  final DateTime? endDate;
  final void Function(DateTime? startDate, DateTime? endDate) onChanged;

  static final DateTime _earliest = DateTime(2000);
  static final DateTime _latest = DateTime(2100);

  Future<DateTime?> _pick(
    BuildContext context,
    DateTime? current, {
    DateTime? first,
    DateTime? last,
  }) {
    final firstDate = first ?? _earliest;
    final lastDate = last ?? _latest;
    final initial = current ?? DateTime.now();
    return showDatePicker(
      context: context,
      initialDate: initial.isBefore(firstDate)
          ? firstDate
          : (initial.isAfter(lastDate) ? lastDate : initial),
      firstDate: firstDate,
      lastDate: lastDate,
    );
  }

  @override
  Widget build(BuildContext context) {
    final start = startDate;
    final end = endDate;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        DateField(
          label: 'Start Date',
          value: start,
          onPick: () => unawaited(() async {
            final picked = await _pick(context, start, last: end);
            if (picked != null) onChanged(picked, end);
          }()),
          onClear: start == null ? null : () => onChanged(null, end),
        ),
        const SizedBox(height: 16),
        DateField(
          label: 'End Date',
          value: end,
          onPick: () => unawaited(() async {
            final picked = await _pick(context, end, first: start);
            if (picked != null) onChanged(start, picked);
          }()),
          onClear: end == null ? null : () => onChanged(start, null),
        ),
      ],
    );
  }
}
