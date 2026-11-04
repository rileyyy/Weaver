import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';

/// A date custom field: a button that opens a date picker, and a button
/// that clears it. Values are `yyyy-MM-dd`, as the server stores them.
class CustomFieldDateEditor extends StatelessWidget {
  const CustomFieldDateEditor({
    super.key,
    required this.value,
    required this.onSave,
  });

  final Object? value;
  final Future<bool> Function(Object? value) onSave;

  static String format(DateTime date) =>
      '${date.year.toString().padLeft(4, '0')}-'
      '${date.month.toString().padLeft(2, '0')}-'
      '${date.day.toString().padLeft(2, '0')}';

  DateTime? get _date => switch (value) {
    final String text => DateTime.tryParse(text),
    _ => null,
  };

  Future<void> _pick(BuildContext context) async {
    final current = _date;
    final picked = await showDatePicker(
      context: context,
      initialDate: current ?? DateTime.now(),
      firstDate: DateTime(1900),
      lastDate: DateTime(2200),
    );
    if (picked != null) await onSave(format(picked));
  }

  @override
  Widget build(BuildContext context) {
    final date = _date;
    return Row(
      children: [
        Expanded(
          child: OutlinedButton.icon(
            icon: const Icon(Icons.event_outlined),
            label: Text(date == null ? 'Pick a date' : formatDate(date)),
            onPressed: () => unawaited(_pick(context)),
          ),
        ),
        if (date != null)
          IconButton(
            icon: const Icon(Icons.clear),
            tooltip: 'Clear date',
            onPressed: () => unawaited(onSave(null)),
          ),
      ],
    );
  }
}
