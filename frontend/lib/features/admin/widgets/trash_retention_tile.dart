import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/admin_server_settings.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/shared/formatting/day_count.dart';

/// The Server tab's row for how many days the Trash keeps items.
class TrashRetentionTile extends StatelessWidget {
  const TrashRetentionTile({
    super.key,
    required this.days,
    required this.onSave,
  });

  final int days;

  /// Takes the text the admin typed; returns null on success or why not.
  final Future<String?> Function(String days) onSave;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      title: const Text('Trash retention'),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(dayCount(days)),
          const Text(
            'Items in the Trash are permanently deleted after this many '
            'days.',
          ),
        ],
      ),
      isThreeLine: true,
      trailing: IconButton(
        tooltip: 'Change Trash retention',
        icon: const Icon(Icons.edit_outlined),
        onPressed: () => unawaited(
          showNameDialog(
            context,
            title: 'Trash retention',
            label:
                'Days, from ${AdminServerSettings.minTrashRetentionDays} '
                'to ${AdminServerSettings.maxTrashRetentionDays}',
            submitLabel: 'Save',
            initialName: '$days',
            onSave: onSave,
          ),
        ),
      ),
    );
  }
}
