import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/shared/models/user_kind.dart';

/// One delete in the Trash tab, with its Restore and Delete permanently
/// buttons.
class TrashedItemTile extends StatelessWidget {
  const TrashedItemTile({
    super.key,
    required this.item,
    required this.onRestore,
    required this.onPurge,
  });

  final TrashedWorkItem item;
  final ValueChanged<TrashedWorkItem> onRestore;
  final ValueChanged<TrashedWorkItem> onPurge;

  @override
  Widget build(BuildContext context) {
    final details = [
      ?item.spaceName,
      _deletion,
      if (item.itemCount > 1) '${item.itemCount} items',
    ].join(' · ');
    return ListTile(
      leading: const Icon(Icons.delete_outline),
      title: Text(
        '#${item.number} ${item.title}',
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: Text(details),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextButton.icon(
            icon: const Icon(Icons.restore_from_trash_outlined),
            label: const Text('Restore'),
            onPressed: () => onRestore(item),
          ),
          IconButton(
            tooltip: 'Delete permanently',
            icon: const Icon(Icons.delete_forever_outlined),
            onPressed: () => onPurge(item),
          ),
        ],
      ),
    );
  }

  /// e.g. "Deleted 2026-11-04 09:05 by claude (agent, tidy-up)".
  String get _deletion {
    final when =
        'Deleted ${formatDate(item.trashedAt)} ${formatTime(item.trashedAt)}';
    final name = item.deletedByName;
    if (name == null) return when;
    final actor = [
      if (item.deletedByKind == UserKind.agent) 'agent',
      ?item.deletedByActorLabel,
    ];
    return actor.isEmpty
        ? '$when by $name'
        : '$when by $name (${actor.join(', ')})';
  }
}
