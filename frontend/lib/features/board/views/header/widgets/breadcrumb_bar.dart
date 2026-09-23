import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';

class BreadcrumbBar extends StatelessWidget {
  const BreadcrumbBar({
    super.key,
    required this.breadcrumbs,
    required this.onSelect,
  });

  final List<ScopeCrumb> breadcrumbs;
  final Future<void> Function(int index) onSelect;

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
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(
                breadcrumbs[i].title,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            )
          else
            TextButton(
              onPressed: () => unawaited(onSelect(i)),
              child: Text(breadcrumbs[i].title),
            ),
        ],
      ],
    );
  }
}
