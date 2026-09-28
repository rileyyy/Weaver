import 'package:flutter/material.dart';

/// A small label in a user row: Admin, Deactivated, Locked, You.
class UserStateBadge extends StatelessWidget {
  const UserStateBadge({
    super.key,
    required this.label,
    this.isWarning = false,
  });

  final String label;
  final bool isWarning;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final background = isWarning
        ? colors.errorContainer
        : colors.secondaryContainer;
    final foreground = isWarning
        ? colors.onErrorContainer
        : colors.onSecondaryContainer;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: Theme.of(
          context,
        ).textTheme.labelSmall?.copyWith(color: foreground),
      ),
    );
  }
}
