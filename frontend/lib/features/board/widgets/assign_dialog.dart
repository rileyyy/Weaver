import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/widgets/user_name_label.dart';

/// Lists every user, marking agents, plus "Unassigned"; tapping an option
/// assigns immediately and closes the dialog. There is no separate
/// OK/Cancel step — picking an option *is* the action.
Future<void> showAssignDialog(
  BuildContext context, {
  required List<User> users,
  required String? currentAssigneeId,
  required Future<void> Function(String? userId) onAssign,
}) {
  return showDialog<void>(
    context: context,
    builder: (dialogContext) => SimpleDialog(
      title: const Text('Assign to'),
      children: [
        _AssignOption(
          initial: null,
          label: const Text('Unassigned'),
          selected: currentAssigneeId == null,
          onSelected: () {
            Navigator.of(dialogContext).pop();
            unawaited(onAssign(null));
          },
        ),
        for (final user in users)
          _AssignOption(
            initial: user.username.isEmpty
                ? null
                : user.username[0].toUpperCase(),
            label: UserNameLabel(user: user),
            selected: user.id == currentAssigneeId,
            onSelected: () {
              Navigator.of(dialogContext).pop();
              unawaited(onAssign(user.id));
            },
          ),
      ],
    ),
  );
}

class _AssignOption extends StatelessWidget {
  const _AssignOption({
    required this.initial,
    required this.label,
    required this.selected,
    required this.onSelected,
  });

  final String? initial;
  final Widget label;
  final bool selected;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    return SimpleDialogOption(
      onPressed: onSelected,
      child: Row(
        children: [
          AssigneeAvatar(initial: initial, showPlaceholderWhenUnassigned: true),
          const SizedBox(width: 12),
          Expanded(
            child: Align(alignment: Alignment.centerLeft, child: label),
          ),
          if (selected) const Icon(Icons.check, size: 18),
        ],
      ),
    );
  }
}
