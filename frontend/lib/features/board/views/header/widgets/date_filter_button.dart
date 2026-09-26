import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';

class DateFilterButton extends StatelessWidget {
  const DateFilterButton({
    super.key,
    required this.label,
    required this.value,
    required this.onPicked,
    required this.onCleared,
    this.firstDate,
    this.lastDate,
  });

  final String label;
  final DateTime? value;
  final ValueChanged<DateTime> onPicked;
  final VoidCallback onCleared;

  /// Bounds for the picker, so the other end of the range can't be crossed.
  final DateTime? firstDate;
  final DateTime? lastDate;

  static final DateTime _earliest = DateTime(2000);
  static final DateTime _latest = DateTime(2100);

  @override
  Widget build(BuildContext context) {
    final current = value;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        TextButton(
          onPressed: () => unawaited(_pick(context)),
          child: Text(current == null ? label : formatDate(current)),
        ),
        if (current != null)
          IconButton(
            icon: const Icon(Icons.close, size: 16),
            tooltip: 'Clear "$label" date',
            visualDensity: VisualDensity.compact,
            onPressed: onCleared,
          ),
      ],
    );
  }

  Future<void> _pick(BuildContext context) async {
    final first = firstDate ?? _earliest;
    final last = lastDate ?? _latest;
    final today = DateTime.now();
    final initial = value ?? today;
    final picked = await showDatePicker(
      context: context,
      initialDate: initial.isBefore(first)
          ? first
          : (initial.isAfter(last) ? last : initial),
      firstDate: first,
      lastDate: last,
    );
    if (picked != null) onPicked(picked);
  }
}
