import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';

/// One labeled date row (used for Start Date / End Date): shows the current
/// value or "Not set", an Edit button to pick a new one, and — only once a
/// value exists — a button to clear it back to unset.
class DateField extends StatelessWidget {
  const DateField({
    super.key,
    required this.label,
    required this.value,
    required this.onPick,
    required this.onClear,
  });

  final String label;
  final DateTime? value;
  final VoidCallback onPick;
  final VoidCallback? onClear;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FieldLabel(label),
        Row(
          children: [
            Expanded(
              child: Text(value == null ? 'Not set' : formatDate(value!)),
            ),
            if (onClear != null)
              TextButton(onPressed: onClear, child: const Text('Clear')),
            TextButton(onPressed: onPick, child: const Text('Edit')),
          ],
        ),
      ],
    );
  }
}
