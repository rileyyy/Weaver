import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/attachment_preview.dart';
import 'package:weaver/features/work_item_detail/widgets/confirm_dialogs.dart';

/// One attachment in the list: tap it to download it, tap its thumbnail to
/// view it full size, or delete it after confirming.
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
    leading: _leading(),
    title: Text(attachment.fileName, overflow: TextOverflow.ellipsis),
    subtitle: Text(attachment.displaySize),
    onTap: onDownload,
    trailing: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        // Tapping the row downloads too, but nothing on the row says so
        // now that its leading slot shows a preview.
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

  Widget _leading() {
    final preview = AttachmentPreview(
      contentType: attachment.contentType,
      thumbnail: thumbnail,
    );
    if (thumbnail == null) return preview;
    return Tooltip(
      message: 'View full size',
      child: InkWell(
        borderRadius: BorderRadius.circular(4),
        onTap: onView,
        child: preview,
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context) async {
    if (!await confirmDeleteAttachment(context, attachment.fileName)) return;
    onDelete();
  }
}
