import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';
import 'package:weaver/features/work_item_detail/widgets/text_entry_row.dart';

class LinksSection extends StatelessWidget {
  const LinksSection({
    super.key,
    required this.links,
    required this.onAdd,
    required this.onRemove,
  });

  final List<WorkItemLink> links;
  final Future<bool> Function(String targetWorkItemId) onAdd;
  final ValueChanged<String> onRemove;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Related Work Items'),
        for (final link in links)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(link.linkedWorkItemTitle),
            trailing: IconButton(
              icon: const Icon(Icons.link_off, size: 18),
              tooltip: 'Remove link',
              onPressed: () => onRemove(link.id),
            ),
          ),
        TextEntryRow(
          hintText: 'Work item id to link',
          buttonLabel: 'Add',
          onSubmit: onAdd,
        ),
      ],
    );
  }
}
