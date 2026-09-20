import 'package:flutter/material.dart';

/// A small 24x24 circle showing the assigned user's initial. Renders nothing
/// if there's no assignee (or the assignee's username hasn't resolved yet),
/// so a card/lane with no assignee doesn't reserve empty space for one.
class AssigneeAvatar extends StatelessWidget {
  const AssigneeAvatar({super.key, required this.initial});

  static const double size = 24;

  final String? initial;

  @override
  Widget build(BuildContext context) {
    final initial = this.initial;
    if (initial == null) return const SizedBox.shrink();

    final colorScheme = Theme.of(context).colorScheme;
    return CircleAvatar(
      radius: size / 2,
      backgroundColor: colorScheme.primaryContainer,
      foregroundColor: colorScheme.onPrimaryContainer,
      child: Text(initial, style: Theme.of(context).textTheme.labelSmall),
    );
  }
}
