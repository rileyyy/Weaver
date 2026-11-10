import 'package:flutter/material.dart';

/// The Filters dialog's switch for Done items the board hides once they
/// were completed longer ago than the Space's done-hiding period.
class DoneFilterBar extends StatelessWidget {
  const DoneFilterBar({
    super.key,
    required this.showDone,
    required this.hidingDays,
    required this.onChanged,
  });

  final bool showDone;

  /// The current Space's done-hiding period, to say what is hidden; null
  /// while no Space is shown.
  final int? hidingDays;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final small = Theme.of(context).textTheme.bodySmall;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Wrap(
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: 8,
          children: [
            Text('Done items', style: small),
            FilterChip(
              label: const Text('Show done'),
              selected: showDone,
              onSelected: onChanged,
            ),
          ],
        ),
        if (!showDone && hidingDays != null)
          Text(_hiddenExplanation(hidingDays!), style: small),
      ],
    );
  }

  static String _hiddenExplanation(int days) =>
      'Hiding items completed more than '
      '${days == 1 ? '1 day' : '$days days'} ago.';
}
