import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// One labeled date row (used for Start Date / End Date): shows the current
/// value or [emptyText], an Edit button to pick a new one, and — only once a
/// value exists — a button to clear it back to unset.
class DateField extends StatelessWidget {
  const DateField({
    super.key,
    required this.label,
    required this.value,
    required this.onPick,
    required this.onClear,
    this.emptyText = 'Not set',
  });

  final String label;
  final DateTime? value;

  /// Null disables the Edit button.
  final VoidCallback? onPick;
  final VoidCallback? onClear;

  /// Shown when [value] is null.
  final String emptyText;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FieldLabel(label),
        Row(
          children: [
            Expanded(
              child: Text(value == null ? emptyText : formatDate(value!)),
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
