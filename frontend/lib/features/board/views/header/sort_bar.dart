import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';

class SortBar extends StatelessWidget {
  const SortBar({super.key, required this.value, required this.onChanged});

  final CardSortOption value;
  final ValueChanged<CardSortOption> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Sort by', style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(width: 8),
        DropdownButton<CardSortOption>(
          value: value,
          onChanged: (option) => option == null ? null : onChanged(option),
          items: [
            for (final option in CardSortOption.values)
              DropdownMenuItem(value: option, child: Text(option.label)),
          ],
        ),
      ],
    );
  }
}
