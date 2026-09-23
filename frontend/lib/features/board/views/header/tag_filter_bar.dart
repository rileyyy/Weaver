import 'package:flutter/material.dart';

class TagFilterBar extends StatelessWidget {
  const TagFilterBar({
    super.key,
    required this.availableTags,
    required this.selectedTags,
    required this.onToggle,
  });

  final List<String> availableTags;
  final Set<String> selectedTags;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    if (availableTags.isEmpty) return const SizedBox.shrink();

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Filter by tag', style: Theme.of(context).textTheme.bodySmall),
        for (final tag in availableTags)
          FilterChip(
            label: Text(tag),
            selected: selectedTags.contains(tag),
            onSelected: (_) => onToggle(tag),
          ),
      ],
    );
  }
}
