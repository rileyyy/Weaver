import 'package:flutter/material.dart';

/// The Filters dialog's switch for Overview's rule: only items in a Doing
/// status or with dates (in the time frame, when one is set).
class ScheduledFilterBar extends StatelessWidget {
  const ScheduledFilterBar({
    super.key,
    required this.only,
    required this.onChanged,
  });

  final bool only;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Scope', style: Theme.of(context).textTheme.bodySmall),
        FilterChip(
          label: const Text('Only in progress or scheduled'),
          tooltip: 'Items in a Doing status, or with dates',
          selected: only,
          onSelected: onChanged,
        ),
      ],
    );
  }
}
