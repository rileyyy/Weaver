import 'package:flutter/material.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

/// Flags a dependency conflict: the item starts before one of its
/// [blockers] ends. Shows nothing when none of them conflicts.
class DependencyConflictBadge extends StatelessWidget {
  const DependencyConflictBadge({
    super.key,
    required this.blockers,
    this.size = 16,
    this.backdrop,
  });

  final List<WorkItemBlocker> blockers;
  final double size;

  /// A disc drawn behind the icon, for placing it over a coloured surface
  /// (such as a Roadmap bar in a red status colour) it would otherwise
  /// blend into.
  final Color? backdrop;

  @override
  Widget build(BuildContext context) {
    final conflicting = [
      for (final blocker in blockers)
        if (blocker.conflicts) blocker,
    ];
    if (conflicting.isEmpty) return const SizedBox.shrink();

    return Tooltip(
      message: [
        for (final blocker in conflicting)
          'Starts before #${blocker.number} ${blocker.title} ends',
      ].join('\n'),
      // Opens on tap as well as hover: the default long press is what
      // picks a board card up on touch platforms.
      triggerMode: TooltipTriggerMode.tap,
      child: _withBackdrop(
        Icon(
          Icons.warning_amber_rounded,
          size: size,
          color: Theme.of(context).colorScheme.error,
          semanticLabel: 'Dependency conflict',
        ),
      ),
    );
  }

  Widget _withBackdrop(Widget icon) {
    final color = backdrop;
    if (color == null) return icon;
    return DecoratedBox(
      decoration: BoxDecoration(color: color, shape: BoxShape.circle),
      child: Padding(padding: const EdgeInsets.all(1), child: icon),
    );
  }
}
