import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/shared/formatting/day_count.dart';
import 'package:weaver/shared/models/space.dart';

/// The Spaces tab's row for one Space: its name and how long its Done items
/// stay on the board, each with its own edit button.
class SpaceTile extends StatelessWidget {
  const SpaceTile({
    super.key,
    required this.space,
    required this.onRename,
    required this.onSetDoneHidingPeriod,
  });

  final Space space;

  /// Each takes the text the admin typed; returns null on success or why
  /// not.
  final Future<String?> Function(String name) onRename;
  final Future<String?> Function(String days) onSetDoneHidingPeriod;

  @override
  Widget build(BuildContext context) {
    final days = space.doneHidingDays;
    return ListTile(
      title: Text(space.name),
      subtitle: Text(
        'Done items leave the board ${dayCount(days)} '
        'after they are completed.',
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            tooltip: 'Change done-hiding period for ${space.name}',
            icon: const Icon(Icons.visibility_off_outlined),
            onPressed: () => unawaited(_changeDoneHidingPeriod(context)),
          ),
          IconButton(
            tooltip: 'Rename ${space.name}',
            icon: const Icon(Icons.edit_outlined),
            onPressed: () => unawaited(_rename(context)),
          ),
        ],
      ),
      onTap: () => unawaited(_rename(context)),
    );
  }

  Future<bool> _rename(BuildContext context) => showNameDialog(
    context,
    title: 'Rename Space',
    label: 'Name',
    submitLabel: 'Rename',
    initialName: space.name,
    onSave: onRename,
  );

  Future<bool> _changeDoneHidingPeriod(BuildContext context) => showNameDialog(
    context,
    title: 'Done-hiding period for ${space.name}',
    label:
        'Days, from ${Space.minDoneHidingDays} '
        'to ${Space.maxDoneHidingDays}',
    submitLabel: 'Save',
    initialName: '${space.doneHidingDays}',
    onSave: onSetDoneHidingPeriod,
  );
}
