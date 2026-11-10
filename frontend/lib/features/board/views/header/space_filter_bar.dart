import 'package:flutter/material.dart';
import 'package:weaver/shared/models/space.dart';

/// The Filters dialog's choice of Spaces. Nothing chosen means every
/// Space, including ones created later, so every chip shows selected then;
/// turning one off chooses all the others. The last chosen Space can't be
/// turned off.
class SpaceFilterBar extends StatelessWidget {
  const SpaceFilterBar({
    super.key,
    required this.spaces,
    required this.selectedIds,
    required this.onChanged,
  });

  final List<Space> spaces;

  /// Empty for every Space.
  final Set<String> selectedIds;
  final ValueChanged<Set<String>> onChanged;

  bool _isShown(Space space) =>
      selectedIds.isEmpty || selectedIds.contains(space.id);

  void _toggle(Space space) {
    final shown = {
      for (final s in spaces)
        if (_isShown(s)) s.id,
    };
    if (!shown.remove(space.id)) shown.add(space.id);
    if (shown.isEmpty) return;
    onChanged(shown.length == spaces.length ? const {} : shown);
  }

  @override
  Widget build(BuildContext context) {
    if (spaces.length < 2) return const SizedBox.shrink();
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Spaces', style: Theme.of(context).textTheme.bodySmall),
        for (final space in spaces)
          FilterChip(
            label: Text(space.name),
            selected: _isShown(space),
            onSelected: (_) => _toggle(space),
          ),
      ],
    );
  }
}
