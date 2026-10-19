import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';

class BreadcrumbBar extends StatelessWidget {
  const BreadcrumbBar({
    super.key,
    required this.breadcrumbs,
    required this.onSelect,
    required this.drillDownTargets,
    required this.onDrillDown,
  });

  final List<ScopeCrumb> breadcrumbs;
  final Future<void> Function(int index) onSelect;

  /// What the current (last) crumb offers to drill into: the swimlanes it
  /// shows, each of which can become the board's new top level.
  final List<ScopeCrumb> drillDownTargets;
  final Future<void> Function(ScopeCrumb target) onDrillDown;

  @override
  Widget build(BuildContext context) {
    final lastIndex = breadcrumbs.length - 1;

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        for (var i = 0; i < breadcrumbs.length; i++) ...[
          if (i > 0)
            Icon(
              Icons.chevron_right,
              size: 18,
              color: Theme.of(context).colorScheme.outline,
            ),
          if (i == lastIndex)
            _currentCrumb(context, breadcrumbs[i])
          else
            TextButton(
              onPressed: () => unawaited(onSelect(i)),
              child: Text(breadcrumbs[i].title),
            ),
        ],
      ],
    );
  }

  Widget _currentCrumb(BuildContext context, ScopeCrumb crumb) {
    final title = Text(
      crumb.title,
      style: Theme.of(context).textTheme.titleLarge,
    );
    if (drillDownTargets.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: title,
      );
    }

    return PopupMenuButton<ScopeCrumb>(
      tooltip: 'Drill down into a lane',
      onSelected: (target) => unawaited(onDrillDown(target)),
      itemBuilder: (_) => [
        for (final target in drillDownTargets)
          PopupMenuItem(value: target, child: Text(target.title)),
      ],
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 4),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [title, const Icon(Icons.arrow_drop_down)],
        ),
      ),
    );
  }
}
