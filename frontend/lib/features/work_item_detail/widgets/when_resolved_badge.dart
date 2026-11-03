import 'package:flutter/material.dart';

/// Whether a When is backed by dates yet.
class WhenResolvedBadge extends StatelessWidget {
  const WhenResolvedBadge({super.key, required this.resolved});

  final bool resolved;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Tooltip(
      message: resolved
          ? 'Backed by dates'
          : 'No dates yet. Pick one below, set the dates, or let an agent '
                'resolve it.',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            resolved ? Icons.event_available : Icons.help_outline,
            size: 16,
            color: resolved ? colorScheme.primary : colorScheme.outline,
          ),
          const SizedBox(width: 4),
          Text(
            resolved ? 'Resolved' : 'Unresolved',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
  }
}
