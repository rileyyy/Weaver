import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

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
        Text(label, style: Theme.of(context).textTheme.labelLarge),
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
