import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';
import 'package:weaver/features/work_item_detail/widgets/text_entry_row.dart';

/// Chips for the current tags plus an input to add one. Adding a tag that
/// only differs in case from an existing one is a no-op, matching the
/// server's case-insensitive de-duplication.
class TagsEditor extends StatelessWidget {
  const TagsEditor({super.key, required this.tags, required this.onChanged});

  final List<String> tags;

  /// Called with the complete new tag list; returns whether it was saved.
  final Future<bool> Function(List<String> tags) onChanged;

  Future<bool> _add(String tag) async {
    final exists = tags.any((t) => t.toLowerCase() == tag.toLowerCase());
    if (exists) return true;
    return onChanged([...tags, tag]);
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Tags'),
        if (tags.isNotEmpty) ...[
          const SizedBox(height: 4),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              for (final tag in tags)
                InputChip(
                  label: Text(tag),
                  labelStyle: TextStyle(
                    color: colorScheme.onSecondaryContainer,
                    fontSize: 12,
                  ),
                  backgroundColor: colorScheme.secondaryContainer,
                  visualDensity: VisualDensity.compact,
                  onDeleted: () => onChanged([
                    for (final existing in tags)
                      if (existing != tag) existing,
                  ]),
                ),
            ],
          ),
        ],
        const SizedBox(height: 8),
        TextEntryRow(hintText: 'Add a tag', buttonLabel: 'Add', onSubmit: _add),
      ],
    );
  }
}
