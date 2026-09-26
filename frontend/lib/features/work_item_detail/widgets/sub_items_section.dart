import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/widgets/field_label.dart';

class SubItemsSection extends StatelessWidget {
  const SubItemsSection({
    super.key,
    required this.children,
    required this.statusColorFor,
    required this.onOpen,
  });

  final List<WorkItemChildSummary> children;
  final Color? Function(String statusId) statusColorFor;
  final ValueChanged<String> onOpen;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Sub-Items'),
        if (children.isEmpty)
          Text('No sub-items', style: Theme.of(context).textTheme.bodySmall)
        else
          for (final child in children)
            ListTile(
              contentPadding: EdgeInsets.zero,
              dense: true,
              leading: Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color:
                      statusColorFor(child.statusId) ??
                      Theme.of(context).colorScheme.outlineVariant,
                  shape: BoxShape.circle,
                ),
              ),
              title: Text(
                '#${child.number} ${child.title}',
                overflow: TextOverflow.ellipsis,
              ),
              onTap: () => onOpen(child.id),
            ),
      ],
    );
  }
}
