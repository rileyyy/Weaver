import 'package:flutter/material.dart';
import 'package:weaver/shared/recurrence/models/weekdays.dart';

/// Seven checkboxes, Monday first, each with its day's initial above it.
/// Each day takes an equal share of the width so the row fits narrow
/// layouts.
class WeekdayPicker extends StatelessWidget {
  const WeekdayPicker({
    super.key,
    required this.selected,
    required this.onToggle,
    this.enabled = true,
  });

  /// [DateTime.monday]..[DateTime.sunday].
  final Set<int> selected;
  final ValueChanged<int> onToggle;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final letterStyle = Theme.of(context).textTheme.labelMedium?.copyWith(
      color: enabled ? null : Theme.of(context).disabledColor,
    );
    return Row(
      children: [
        for (final day in weekdaysMondayFirst)
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(weekdayLetter(day), style: letterStyle),
                Checkbox(
                  value: selected.contains(day),
                  semanticLabel: weekdayToWire(day),
                  visualDensity: VisualDensity.compact,
                  materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  onChanged: enabled ? (_) => onToggle(day) : null,
                ),
              ],
            ),
          ),
      ],
    );
  }
}
