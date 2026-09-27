import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/widgets/weekday_picker.dart';
import 'package:weaver/shared/widgets/date_field.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The repeat settings form: frequency, weekdays and the start/end of the
/// repetition. Holds no state of its own; every edit is reported through
/// [onChanged] and saving is up to the caller.
class RecurrenceEditor extends StatelessWidget {
  const RecurrenceEditor({
    super.key,
    required this.draft,
    required this.onChanged,
    this.enabled = true,
  });

  final RecurrenceDraft draft;
  final ValueChanged<RecurrenceDraft> onChanged;
  final bool enabled;

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
    final initial = current ?? draft.startDate;
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
    final weekBased = draft.frequency.isWeekBased;
    final end = draft.endDate;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Repeats'),
        DropdownButtonFormField<RecurrenceFrequency>(
          // Keyed so a reset from outside (discard, reload) shows through.
          key: ValueKey(draft.frequency),
          initialValue: draft.frequency,
          decoration: const InputDecoration(),
          items: [
            for (final frequency in RecurrenceFrequency.values)
              DropdownMenuItem(value: frequency, child: Text(frequency.label)),
          ],
          onChanged: enabled
              ? (value) {
                  if (value != null) onChanged(draft.withFrequency(value));
                }
              : null,
        ),
        const SizedBox(height: 12),
        const FieldLabel('On'),
        WeekdayPicker(
          selected: draft.weekdays,
          enabled: enabled && weekBased,
          onToggle: (day) => onChanged(draft.toggleWeekday(day)),
        ),
        if (!weekBased)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(
              'Repeats on day ${draft.startDate.day} of the month '
              '(or the last day, in shorter months).',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        const SizedBox(height: 12),
        DateField(
          label: 'Repeat Start',
          value: draft.startDate,
          onPick: enabled
              ? () => unawaited(() async {
                  final picked = await _pick(
                    context,
                    draft.startDate,
                    last: end,
                  );
                  if (picked != null) {
                    onChanged(draft.copyWith(startDate: picked));
                  }
                }())
              : null,
          onClear: null,
        ),
        const SizedBox(height: 12),
        DateField(
          label: 'Repeat End',
          value: end,
          emptyText: 'No end date',
          onPick: enabled
              ? () => unawaited(() async {
                  final picked = await _pick(
                    context,
                    end,
                    first: draft.startDate,
                  );
                  if (picked != null) {
                    onChanged(draft.copyWith(endDate: () => picked));
                  }
                }())
              : null,
          onClear: enabled && end != null
              ? () => onChanged(draft.copyWith(endDate: () => null))
              : null,
        ),
      ],
    );
  }
}
