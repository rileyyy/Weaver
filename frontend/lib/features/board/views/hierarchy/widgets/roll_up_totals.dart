import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/hierarchy_roll_up.dart';

/// A Hierarchy row's roll-ups on one line, each field's own value and its
/// sub-items' total named apart ("Budget 1000.00 · sub-items 250.00"). A
/// tooltip gives one line per field when the column is too narrow.
class RollUpTotals extends StatelessWidget {
  const RollUpTotals({super.key, required this.rollUps});

  final List<HierarchyRollUp> rollUps;

  @override
  Widget build(BuildContext context) {
    if (rollUps.isEmpty) return const SizedBox.shrink();
    final lines = [for (final rollUp in rollUps) describe(rollUp)];
    return Tooltip(
      message: lines.join('\n'),
      child: Text(
        lines.join('; '),
        overflow: TextOverflow.ellipsis,
        style: Theme.of(context).textTheme.bodySmall,
      ),
    );
  }

  static String describe(HierarchyRollUp rollUp) {
    final kind = rollUp.field.kind;
    final parts = [
      if (rollUp.ownValue case final own?) kind.formatAmount(own),
      if (rollUp.total case final total?)
        'sub-items ${kind.formatAmount(total)}',
    ];
    return '${rollUp.field.name} ${parts.join(' · ')}';
  }
}
