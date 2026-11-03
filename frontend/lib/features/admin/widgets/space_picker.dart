import 'package:flutter/material.dart';
import 'package:weaver/shared/models/space.dart';

/// Chooses which Space's values an Admin tab manages, for lookups each Space
/// owns its own of (statuses, priorities).
class SpacePicker extends StatelessWidget {
  const SpacePicker({
    super.key,
    required this.spaces,
    required this.selectedId,
    required this.onSelected,
  });

  final List<Space> spaces;
  final String? selectedId;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    if (spaces.isEmpty) {
      return const Text('Create a Space on the Spaces tab first.');
    }
    return Row(
      spacing: 12,
      children: [
        const Text('Space'),
        DropdownButton<String>(
          value: selectedId,
          items: [
            for (final space in spaces)
              DropdownMenuItem(value: space.id, child: Text(space.name)),
          ],
          onChanged: (id) {
            if (id != null) onSelected(id);
          },
        ),
      ],
    );
  }
}
