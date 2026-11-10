import 'package:flutter/material.dart';
import 'package:weaver/shared/models/space.dart';

/// The heading over one Space's part of a layout while several Spaces are
/// shown. Tapping it shows only that Space.
class SpaceSectionHeader extends StatelessWidget {
  const SpaceSectionHeader({
    super.key,
    required this.space,
    required this.onTap,
    this.height,
  });

  final Space space;
  final VoidCallback onTap;

  /// Fixed for layouts whose rows all share one height (the Roadmap).
  final double? height;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Material(
      color: theme.colorScheme.secondaryContainer,
      child: InkWell(
        onTap: onTap,
        child: SizedBox(
          height: height,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Row(
              children: [
                Icon(
                  Icons.folder_outlined,
                  size: 18,
                  color: theme.colorScheme.onSecondaryContainer,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    space.name,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: theme.colorScheme.onSecondaryContainer,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
