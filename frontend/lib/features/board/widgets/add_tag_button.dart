import 'package:flutter/material.dart';

/// The round "+" that ends a [TagBadgeRow] when tags can be added from
/// that row. Sized like a compact avatar rather than a tag badge: a
/// badge-sized "+" was too small a target to hit reliably, even with a mouse.
class AddTagButton extends StatelessWidget {
  const AddTagButton({super.key, required this.onPressed});

  static const double size = 22;

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Tooltip(
      message: 'Add tag',
      child: Material(
        color: colorScheme.surfaceContainerHighest,
        shape: const CircleBorder(),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onPressed,
          child: SizedBox.square(
            dimension: size,
            child: Icon(
              Icons.add,
              size: 16,
              color: colorScheme.onSurfaceVariant,
            ),
          ),
        ),
      ),
    );
  }
}
