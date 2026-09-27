import 'package:flutter/material.dart';

/// Asks before stopping a work item repeating. True means stop.
Future<bool> confirmStopRepeating(BuildContext context) async =>
    await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Stop repeating?'),
        content: const Text(
          'No more copies of this item will be created. Copies that already '
          'exist are kept.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Stop repeating'),
          ),
        ],
      ),
    ) ??
    false;
