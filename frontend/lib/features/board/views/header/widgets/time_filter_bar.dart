import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/header/widgets/date_filter_button.dart';

class TimeFilterBar extends StatelessWidget {
  const TimeFilterBar({super.key, 
    required this.start,
    required this.end,
    required this.onChanged,
    required this.onClear,
  });

  final DateTime? start;
  final DateTime? end;
  final void Function({DateTime? start, DateTime? end}) onChanged;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Show items active', style: Theme.of(context).textTheme.bodySmall),
        DateFilterButton(
          label: 'from',
          value: start,
          onPicked: (picked) => onChanged(start: picked, end: end),
        ),
        DateFilterButton(
          label: 'to',
          value: end,
          onPicked: (picked) => onChanged(start: start, end: picked),
        ),
        if (start != null || end != null)
          TextButton(onPressed: onClear, child: const Text('Clear')),
      ],
    );
  }
}
