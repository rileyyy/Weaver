import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/attachment_preview.dart';
import 'package:weaver/features/work_item_detail/widgets/confirm_dialogs.dart';

/// One attachment in the list: tap an image to view it full size or any
/// other file to download it, download either with its button, or delete it
/// after confirming.
class AttachmentTile extends StatelessWidget {
  const AttachmentTile({
    super.key,
    required this.attachment,
    required this.thumbnail,
    required this.onView,
    required this.onDownload,
    required this.onDelete,
  });

  final WorkItemAttachment attachment;

  /// Null when the attachment has no thumbnail, which also means it can't
  /// be viewed in the app.
  final Future<Uint8List>? thumbnail;

  final VoidCallback onView;
  final VoidCallback onDownload;

  /// Called only once the user has confirmed.
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) => ListTile(
    contentPadding: EdgeInsets.zero,
    dense: true,
    leading: AttachmentPreview(
      contentType: attachment.contentType,
      thumbnail: thumbnail,
    ),
    title: Text(attachment.fileName, overflow: TextOverflow.ellipsis),
    subtitle: Text(attachment.displaySize),
    onTap: thumbnail == null ? onDownload : onView,
    trailing: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        // An image's row opens the viewer, so this is its only way to
        // download.
        IconButton(
          icon: const Icon(Icons.download, size: 18),
          tooltip: 'Download attachment',
          onPressed: onDownload,
        ),
        IconButton(
          icon: const Icon(Icons.delete_outline, size: 18),
          tooltip: 'Delete attachment',
          onPressed: () => unawaited(_confirmDelete(context)),
        ),
      ],
    ),
  );

  Future<void> _confirmDelete(BuildContext context) async {
    if (!await confirmDeleteAttachment(context, attachment.fileName)) return;
    onDelete();
  }
}
