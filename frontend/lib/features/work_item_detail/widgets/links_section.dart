import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// This item's links to other work items, grouped by type as read from
/// this item. Adding one picks the type here and leaves choosing the other
/// item to [onAdd].
class LinksSection extends StatefulWidget {
  const LinksSection({
    super.key,
    required this.links,
    required this.onAdd,
    required this.onRemove,
    required this.onOpen,
  });

  final List<WorkItemLink> links;

  /// Called with the chosen type when the user asks to add a link.
  final ValueChanged<WorkItemLinkRelation> onAdd;

  /// Called with the link's id.
  final ValueChanged<String> onRemove;

  /// Called with the other item's id.
  final ValueChanged<String> onOpen;

  @override
  State<LinksSection> createState() => _LinksSectionState();
}

class _LinksSectionState extends State<LinksSection> {
  // Blocking links come first: they're the ones that change what to do next.
  static const List<WorkItemLinkRelation> _groupOrder = [
    WorkItemLinkRelation.blocks,
    WorkItemLinkRelation.blockedBy,
    WorkItemLinkRelation.related,
    WorkItemLinkRelation.duplicates,
    WorkItemLinkRelation.duplicatedBy,
  ];

  WorkItemLinkRelation _newLinkType = WorkItemLinkRelation.related;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Linked Work Items'),
        if (widget.links.isEmpty)
          Text('No linked work items', style: textTheme.bodySmall),
        for (final type in _groupOrder) ..._group(type, textTheme),
        _addRow(),
      ],
    );
  }

  List<Widget> _group(WorkItemLinkRelation type, TextTheme textTheme) {
    final links = [
      for (final link in widget.links)
        if (link.type == type) link,
    ];
    if (links.isEmpty) return const [];
    return [
      Padding(
        padding: const EdgeInsets.only(top: 8),
        child: Text(type.label, style: textTheme.labelMedium),
      ),
      for (final link in links) _linkTile(link),
    ];
  }

  Widget _linkTile(WorkItemLink link) => ListTile(
    contentPadding: EdgeInsets.zero,
    dense: true,
    title: Text(
      '#${link.linkedWorkItemNumber} ${link.linkedWorkItemTitle}',
      overflow: TextOverflow.ellipsis,
    ),
    onTap: () => widget.onOpen(link.linkedWorkItemId),
    trailing: IconButton(
      icon: const Icon(Icons.link_off, size: 18),
      tooltip: 'Remove link',
      onPressed: () => widget.onRemove(link.id),
    ),
  );

  Widget _addRow() => Row(
    children: [
      Expanded(
        child: DropdownButton<WorkItemLinkRelation>(
          key: const Key('link-type-picker'),
          value: _newLinkType,
          isExpanded: true,
          items: [
            for (final type in WorkItemLinkRelation.values)
              DropdownMenuItem(value: type, child: Text(type.label)),
          ],
          onChanged: (type) {
            if (type != null) setState(() => _newLinkType = type);
          },
        ),
      ),
      const SizedBox(width: 8),
      TextButton(
        onPressed: () => widget.onAdd(_newLinkType),
        child: const Text('Add link'),
      ),
    ],
  );
}
