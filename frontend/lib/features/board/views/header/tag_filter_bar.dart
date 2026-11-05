import 'package:flutter/material.dart';

import 'package:weaver/shared/models/tag_match.dart';

class TagFilterBar extends StatelessWidget {
  const TagFilterBar({
    super.key,
    required this.availableTags,
    required this.isSelected,
    required this.onToggle,
    required this.tagMatch,
    required this.onTagMatchChanged,
  });

  final List<String> availableTags;
  final bool Function(String tag) isSelected;
  final ValueChanged<String> onToggle;
  final TagMatch tagMatch;
  final ValueChanged<TagMatch> onTagMatchChanged;

  @override
  Widget build(BuildContext context) {
    if (availableTags.isEmpty) return const SizedBox.shrink();

    // Any and All only differ once more than one tag is selected.
    final showMatchControl = availableTags.where(isSelected).length >= 2;

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      runSpacing: 4,
      children: [
        Text('Filter by tag', style: Theme.of(context).textTheme.bodySmall),
        if (showMatchControl) _buildMatchSelector(),
        for (final tag in availableTags)
          FilterChip(
            label: Text(tag),
            selected: isSelected(tag),
            onSelected: (_) => onToggle(tag),
          ),
      ],
    );
  }

  Widget _buildMatchSelector() {
    return SegmentedButton<TagMatch>(
      showSelectedIcon: false,
      style: const ButtonStyle(
        visualDensity: VisualDensity.compact,
        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
      ),
      segments: const [
        ButtonSegment(
          value: TagMatch.any,
          label: Text('Any'),
          tooltip: 'Show items with any of the selected tags',
        ),
        ButtonSegment(
          value: TagMatch.all,
          label: Text('All'),
          tooltip: 'Show only items with all of the selected tags',
        ),
      ],
      selected: {tagMatch},
      onSelectionChanged: (selection) => onTagMatchChanged(selection.single),
    );
  }
}
