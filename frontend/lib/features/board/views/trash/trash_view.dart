import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/features/board/trash_view_model.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// The Trash tab: each delete, with who made it and when, and restore.
/// Purging is ticket 20.
class TrashView extends StatelessWidget {
  const TrashView({
    super.key,
    required this.viewModel,
    required this.onRestored,
  });

  final TrashViewModel viewModel;

  /// Called after a restore succeeds, so the board can show the item again.
  final VoidCallback onRestored;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        if (viewModel.loadError case final error?) {
          return LoadErrorView(message: error, onRetry: viewModel.load);
        }
        final items = viewModel.items;
        if (items.isEmpty) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text('The Trash is empty.', textAlign: TextAlign.center),
            ),
          );
        }
        return ListView.separated(
          padding: const EdgeInsets.symmetric(vertical: 8),
          itemCount: items.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) => _TrashedItemTile(
            item: items[index],
            onRestore: (item) => unawaited(_restore(context, item)),
          ),
        );
      },
    );
  }

  Future<void> _restore(BuildContext context, TrashedWorkItem item) async {
    if (await viewModel.restore(item.workItemId)) {
      onRestored();
      return;
    }
    final error = viewModel.actionError;
    viewModel.clearActionError();
    if (error != null && context.mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(error)));
    }
  }
}

class _TrashedItemTile extends StatelessWidget {
  const _TrashedItemTile({required this.item, required this.onRestore});

  final TrashedWorkItem item;
  final ValueChanged<TrashedWorkItem> onRestore;

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
      trailing: TextButton.icon(
        icon: const Icon(Icons.restore_from_trash_outlined),
        label: const Text('Restore'),
        onPressed: () => onRestore(item),
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
