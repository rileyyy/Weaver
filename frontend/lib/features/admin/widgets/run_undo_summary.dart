import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/run_undo_result.dart';

/// How undoing a run went: how many changes were undone and comments kept,
/// then each change left as it is, with why.
class RunUndoSummary extends StatelessWidget {
  const RunUndoSummary({super.key, required this.result});

  final RunUndoResult result;

  @override
  Widget build(BuildContext context) {
    final small = Theme.of(context).textTheme.bodySmall;
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(_headline),
          for (final change in result.skipped)
            Padding(
              padding: const EdgeInsets.only(left: 12, top: 4),
              child: Text(
                '${change.itemName}, ${change.fieldLabel}: '
                'not undone, ${change.outcome.skipReason}',
                style: small,
              ),
            ),
        ],
      ),
    );
  }

  String get _headline {
    final undone = _plural(result.undoneCount, 'change');
    final parts = ['Undone: $undone.'];
    if (result.keptCount > 0) {
      parts.add('Kept ${_plural(result.keptCount, 'comment')}.');
    }
    if (result.skipped.isNotEmpty) {
      parts.add('Left ${result.skipped.length} as they are:');
    }
    return parts.join(' ');
  }

  static String _plural(int count, String noun) =>
      count == 1 ? '1 $noun' : '$count ${noun}s';
}
