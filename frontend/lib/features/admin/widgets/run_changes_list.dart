import 'package:flutter/material.dart';
import 'package:weaver/features/admin/state/run_changes.dart';
import 'package:weaver/features/admin/widgets/run_item_changes.dart';

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
      children: [for (final item in changes.items) RunItemChanges(item: item)],
    );
  }
}
