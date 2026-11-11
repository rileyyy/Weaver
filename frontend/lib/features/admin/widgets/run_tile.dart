import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/state/run_changes.dart';
import 'package:weaver/features/admin/widgets/run_changes_list.dart';

/// One run on the Runs tab: the agent and when the run started, collapsed;
/// its changes once expanded. [onExpand] asks for the changes, which arrive
/// as [changes].
class RunTile extends StatelessWidget {
  const RunTile({
    super.key,
    required this.run,
    required this.changes,
    required this.onExpand,
  });

  final RunReport run;
  final RunChanges? changes;
  final VoidCallback onExpand;

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
          if (run.apiKeyLabel case final key?) 'key "$key"',
        ].join(' · '),
      ),
      onExpansionChanged: (expanded) {
        if (expanded) onExpand();
      },
      expandedCrossAxisAlignment: CrossAxisAlignment.start,
      childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
      children: [RunChangesList(changes: changes, onRetry: onExpand)],
    );
  }
}
