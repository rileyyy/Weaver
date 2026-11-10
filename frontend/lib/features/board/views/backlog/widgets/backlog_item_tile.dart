import 'package:flutter/material.dart';
import 'package:weaver/shared/models/work_item.dart';

/// One Backlog item, with a button to give it dates.
class BacklogItemTile extends StatelessWidget {
  const BacklogItemTile({
    super.key,
    required this.item,
    required this.onOpen,
    required this.onSetDates,
  });

  final WorkItem item;
  final VoidCallback onOpen;
  final VoidCallback onSetDates;

  @override
  Widget build(BuildContext context) {
    final when = item.when;
    return ListTile(
      contentPadding: const EdgeInsets.only(left: 32, right: 16),
      title: Text(
        '#${item.number} ${item.title}',
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: when == null || when.isEmpty
          ? null
          : Text(when, overflow: TextOverflow.ellipsis),
      onTap: onOpen,
      trailing: TextButton.icon(
        icon: const Icon(Icons.event_outlined),
        label: const Text('Set dates'),
        onPressed: onSetDates,
      ),
    );
  }
}
