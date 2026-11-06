import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/shared/widgets/confirm_action.dart';

/// Asks before purging one delete. True means go ahead.
Future<bool> confirmPurge(BuildContext context, TrashedWorkItem item) {
  final name = '#${item.number} ${item.title}';
  final what = item.itemCount > 1
      ? '"$name" and everything deleted with it (${item.itemCount} items) '
            'will be'
      : '"$name" will be';
  return confirmAction(
    context,
    title: 'Delete permanently?',
    message:
        '$what deleted permanently, with comments, links and attachments. '
        "This can't be undone.",
    confirmLabel: 'Delete permanently',
    isDestructive: true,
  );
}

/// Asks before purging everything in the Trash. True means go ahead.
Future<bool> confirmEmptyTrash(BuildContext context) => confirmAction(
  context,
  title: 'Empty the Trash?',
  message:
      'Everything in the Trash, in every Space, will be deleted permanently, '
      "with comments, links and attachments. This can't be undone.",
  confirmLabel: 'Empty Trash',
  isDestructive: true,
);
