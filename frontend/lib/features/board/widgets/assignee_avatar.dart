import 'package:flutter/material.dart';

/// A small circle showing the assigned user's initial, [defaultSize] (24px)
/// unless [size] overrides it. With no assignee, renders nothing unless
/// [showPlaceholderWhenUnassigned] is set, in which case it shows a plain
/// silhouette instead — used where "nobody assigned yet" still needs a
/// visible, tappable target (e.g. a swimlane label), as opposed to a card,
/// which stays compact when it has no assignee to show.
class AssigneeAvatar extends StatelessWidget {
  const AssigneeAvatar({
    super.key,
    required this.initial,
    this.size = defaultSize,
    this.showPlaceholderWhenUnassigned = false,
    this.onTap,
  });

  static const double defaultSize = 24;

  /// The size [BoardCard] renders its own avatar at — 1.34x the default
  /// (roughly the doubled size from before, reduced by a third per
  /// follow-up feedback that the doubled avatar was too big). Shared with
  /// the swimlane label so both read as the same visual weight.
  static const double cardSize = defaultSize * 4 / 3;

  final String? initial;
  final double size;
  final bool showPlaceholderWhenUnassigned;

  /// Opens an assign dialog when tapped — null leaves the avatar
  /// non-interactive.
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final initial = this.initial;
    if (initial == null && !showPlaceholderWhenUnassigned) {
      return const SizedBox.shrink();
    }

    final colorScheme = Theme.of(context).colorScheme;
    final avatar = initial == null
        ? CircleAvatar(
            radius: size / 2,
            backgroundColor: colorScheme.surfaceContainerHighest,
            foregroundColor: colorScheme.onSurfaceVariant,
            child: Icon(Icons.person_outline, size: size * 0.65),
          )
        : CircleAvatar(
            radius: size / 2,
            backgroundColor: colorScheme.primaryContainer,
            foregroundColor: colorScheme.onPrimaryContainer,
            child: Text(
              initial,
              style: Theme.of(
                context,
              ).textTheme.labelSmall?.copyWith(fontSize: size * 0.5),
            ),
          );

    final onTap = this.onTap;
    if (onTap == null) return avatar;
    return InkWell(
      borderRadius: BorderRadius.circular(size / 2),
      onTap: onTap,
      child: avatar,
    );
  }
}
