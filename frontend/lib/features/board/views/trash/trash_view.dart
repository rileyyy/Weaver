import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/features/board/trash_view_model.dart';
import 'package:weaver/features/board/views/trash/widgets/confirm_purge.dart';
import 'package:weaver/features/board/views/trash/widgets/trashed_item_tile.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// The Trash tab: each delete, with who made it and when, restore, and
/// permanent deletion of one delete or the whole Trash.
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
        return Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
              child: TextButton.icon(
                icon: const Icon(Icons.delete_sweep_outlined),
                label: const Text('Empty Trash'),
                onPressed: () => unawaited(_emptyTrash(context)),
              ),
            ),
            Expanded(child: _list(items)),
          ],
        );
      },
    );
  }

  Widget _list(List<TrashedWorkItem> items) => ListView.separated(
    padding: const EdgeInsets.symmetric(vertical: 8),
    itemCount: items.length,
    separatorBuilder: (_, _) => const Divider(height: 1),
    itemBuilder: (context, index) => TrashedItemTile(
      item: items[index],
      onRestore: (item) => unawaited(_restore(context, item)),
      onPurge: (item) => unawaited(_purge(context, item)),
    ),
  );

  Future<void> _restore(BuildContext context, TrashedWorkItem item) async {
    if (await viewModel.restore(item.workItemId)) {
      onRestored();
      return;
    }
    if (context.mounted) _showActionError(context);
  }

  Future<void> _purge(BuildContext context, TrashedWorkItem item) async {
    if (!await confirmPurge(context, item)) return;
    if (await viewModel.purge(item.workItemId)) return;
    if (context.mounted) _showActionError(context);
  }

  Future<void> _emptyTrash(BuildContext context) async {
    if (!await confirmEmptyTrash(context)) return;
    if (await viewModel.emptyTrash()) return;
    if (context.mounted) _showActionError(context);
  }

  void _showActionError(BuildContext context) {
    final error = viewModel.actionError;
    viewModel.clearActionError();
    if (error == null) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
  }
}
