import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/run_changed_item.dart';

/// One item a run changed: its name, marked if the run deleted it, then each
/// change the run made to it (field, old → new) with the actor label sent.
class RunItemChanges extends StatelessWidget {
  const RunItemChanges({super.key, required this.item});

  final RunChangedItem item;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context).textTheme;
    final small = theme.bodySmall;
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            item.isDeleted ? '${item.name} (deleted)' : item.name,
            style: theme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
          ),
          for (final change in item.changes)
            Padding(
              padding: const EdgeInsets.only(left: 12, top: 4),
              child: Wrap(
                spacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(change.change),
                  if (change.actorLabel case final label?)
                    Text(label, style: small),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
