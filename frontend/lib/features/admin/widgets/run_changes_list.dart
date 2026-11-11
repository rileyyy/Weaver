import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/run_changed_item.dart';
import 'package:weaver/features/admin/state/run_changes.dart';

/// An expanded run's changes: each item it changed, then each change to it
/// (field, old → new) with the actor label the agent sent.
class RunChangesList extends StatelessWidget {
  const RunChangesList({
    super.key,
    required this.changes,
    required this.onRetry,
  });

  /// Null until the changes are first asked for.
  final RunChanges? changes;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final changes = this.changes;
    if (changes == null || changes.isLoading) {
      return const Padding(
        padding: EdgeInsets.all(8),
        child: Center(child: CircularProgressIndicator()),
      );
    }
    if (changes.error case final error?) {
      return Row(
        children: [
          Expanded(child: Text(error)),
          TextButton(onPressed: onRetry, child: const Text('Retry')),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [for (final item in changes.items) _ItemChanges(item: item)],
    );
  }
}

class _ItemChanges extends StatelessWidget {
  const _ItemChanges({required this.item});

  final RunChangedItem item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context).textTheme;
    final small = theme.bodySmall;
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            item.isDeleted ? '${item.name} (deleted)' : item.name,
            style: theme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
          ),
          for (final change in item.changes)
            Padding(
              padding: const EdgeInsets.only(left: 12, top: 4),
              child: Wrap(
                spacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(change.change),
                  if (change.actorLabel case final label?)
                    Text(label, style: small),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
