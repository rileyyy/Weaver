import 'package:flutter/material.dart';

/// Whether a When is backed by dates yet, or was edited since and awaits an
/// agent resolving it again.
class WhenResolvedBadge extends StatelessWidget {
  const WhenResolvedBadge({
    super.key,
    required this.resolved,
    this.awaitingReResolution = false,
  });

  final bool resolved;
  final bool awaitingReResolution;

  @override
  Widget build(BuildContext context) {
    if (awaitingReResolution) return _awaitingReResolution(context);

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

  Widget _awaitingReResolution(BuildContext context) => Tooltip(
    message:
        'The phrase changed after the dates were set. The dates stay until an '
        'agent resolves it again, or you change them.',
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          Icons.update,
          size: 16,
          color: Theme.of(context).colorScheme.tertiary,
        ),
        const SizedBox(width: 4),
        Text(
          'Awaiting re-resolution',
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
    ),
  );
}
