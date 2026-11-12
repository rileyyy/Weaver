import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/state/run_changes.dart';
import 'package:weaver/features/admin/state/run_undo.dart';
import 'package:weaver/features/admin/widgets/run_changes_list.dart';
import 'package:weaver/features/admin/widgets/run_undo_panel.dart';

/// One run on the Runs tab: the agent and when the run started, collapsed;
/// its changes once expanded, and a way to undo it. [onExpand] asks for the
/// changes, which arrive as [changes]; [onUndo] asks to undo the run, whose
/// progress arrives as [undo].
class RunTile extends StatelessWidget {
  const RunTile({
    super.key,
    required this.run,
    required this.changes,
    required this.onExpand,
    required this.undo,
    required this.onUndo,
  });

  final RunReport run;
  final RunChanges? changes;
  final VoidCallback onExpand;
  final RunUndo? undo;
  final VoidCallback onUndo;

  @override
  Widget build(BuildContext context) {
    final started = run.firstChangeAt;
    final count = run.changeCount == 1
        ? '1 change'
        : '${run.changeCount} changes';
    return ExpansionTile(
      title: Text(run.accountName),
      subtitle: Text(
        [
          '${formatDate(started)} ${formatTime(started)}',
          count,
          'key "${run.apiKeyLabel}"',
        ].join(' · '),
      ),
      onExpansionChanged: (expanded) {
        if (expanded) onExpand();
      },
      expandedCrossAxisAlignment: CrossAxisAlignment.start,
      childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      children: [
        RunChangesList(changes: changes, onRetry: onExpand),
        RunUndoPanel(undo: undo, onUndo: onUndo),
      ],
    );
  }
}
