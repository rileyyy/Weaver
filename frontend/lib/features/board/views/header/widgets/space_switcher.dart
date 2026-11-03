import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/shared/models/space.dart';

/// Switches the board to another Space. The current Space's name is the
/// root breadcrumb right after it, so this stays a compact icon.
class SpaceSwitcher extends StatelessWidget {
  const SpaceSwitcher({
    super.key,
    required this.spaces,
    required this.current,
    required this.onSwitch,
  });

  final List<Space> spaces;
  final Space? current;
  final Future<void> Function(Space space) onSwitch;

  @override
  Widget build(BuildContext context) {
    if (spaces.length < 2) return const SizedBox.shrink();

    return PopupMenuButton<Space>(
      tooltip: 'Switch Space',
      icon: const Icon(Icons.swap_horiz),
      onSelected: (space) => unawaited(onSwitch(space)),
      itemBuilder: (_) => [
        for (final space in spaces)
          PopupMenuItem(
            value: space,
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: space.id == current?.id
                  ? const Icon(Icons.check)
                  : const SizedBox(width: 24),
              title: Text(space.name),
            ),
          ),
      ],
    );
  }
}
