import 'package:flutter/material.dart';
import 'package:weaver/features/admin/state/run_undo.dart';
import 'package:weaver/features/admin/widgets/run_undo_summary.dart';

/// Below an expanded run's changes: a button to undo the run, then how the
/// undo went, with each change it left alone and why.
class RunUndoPanel extends StatelessWidget {
  const RunUndoPanel({super.key, required this.undo, required this.onUndo});

  /// Null until the run is first undone.
  final RunUndo? undo;
  final VoidCallback onUndo;

  @override
  Widget build(BuildContext context) {
    final undo = this.undo;
    if (undo == null) {
      return TextButton.icon(
        onPressed: onUndo,
        icon: const Icon(Icons.undo),
        label: const Text('Undo this run'),
      );
    }
    if (undo.isInProgress) {
      return const Padding(
        padding: EdgeInsets.all(8),
        child: Center(child: CircularProgressIndicator()),
      );
    }
    if (undo.error case final error?) {
      return Row(
        children: [
          Expanded(child: Text(error)),
          TextButton(onPressed: onUndo, child: const Text('Retry')),
        ],
      );
    }
    return RunUndoSummary(result: undo.result!);
  }
}
