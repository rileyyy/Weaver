import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/attachment_image_viewer.dart';
import 'package:weaver/features/work_item_detail/widgets/attachment_tile.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The files attached to this item: download one by tapping it, view an
/// image full size by tapping its thumbnail, delete one (after confirming),
/// or attach another, from a file or, on a phone, the camera.
class AttachmentsSection extends StatelessWidget {
  const AttachmentsSection({
    super.key,
    required this.attachments,
    required this.isLoading,
    required this.loadError,
    required this.isUploading,
    required this.error,
    required this.canTakePhoto,
    required this.onUpload,
    required this.onTakePhoto,
    required this.onDownload,
    required this.onDelete,
    required this.thumbnailOf,
    required this.imageOf,
  });

  final List<WorkItemAttachment> attachments;
  final bool isLoading;
  final String? loadError;
  final bool isUploading;

  /// Why the last upload, download or delete failed.
  final String? error;

  /// Whether to offer "Take photo" beside "Attach file".
  final bool canTakePhoto;

  final VoidCallback onUpload;
  final VoidCallback onTakePhoto;
  final ValueChanged<WorkItemAttachment> onDownload;

  /// Called only once the user has confirmed.
  final ValueChanged<WorkItemAttachment> onDelete;

  /// Asked only for attachments with a thumbnail, on every build, so it
  /// should hand back the same future for the same attachment.
  final Future<Uint8List> Function(WorkItemAttachment) thumbnailOf;

  /// The full-size image, fetched when the viewer opens.
  final Future<Uint8List> Function(WorkItemAttachment) imageOf;

  @override
  Widget build(BuildContext context) {
    final errorStyle = TextStyle(color: Theme.of(context).colorScheme.error);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Attachments'),
        ..._content(context, errorStyle),
        if (error case final message?) Text(message, style: errorStyle),
      ],
    );
  }

  List<Widget> _content(BuildContext context, TextStyle errorStyle) {
    if (loadError case final message?) {
      return [Text(message, style: errorStyle)];
    }
    if (isLoading) return const [LinearProgressIndicator()];
    return [
      for (final attachment in attachments) _tile(context, attachment),
      Wrap(
        children: [
          TextButton.icon(
            onPressed: isUploading ? null : onUpload,
            icon: const Icon(Icons.attach_file, size: 18),
            label: Text(isUploading ? 'Uploading…' : 'Attach file'),
          ),
          if (canTakePhoto)
            TextButton.icon(
              onPressed: isUploading ? null : onTakePhoto,
              icon: const Icon(Icons.photo_camera_outlined, size: 18),
              label: const Text('Take photo'),
            ),
        ],
      ),
    ];
  }

  Widget _tile(BuildContext context, WorkItemAttachment attachment) =>
      AttachmentTile(
        attachment: attachment,
        thumbnail: attachment.hasThumbnail ? thumbnailOf(attachment) : null,
        onView: () => unawaited(
          AttachmentImageViewer.show(
            context,
            fileName: attachment.fileName,
            load: () => imageOf(attachment),
          ),
        ),
        onDownload: () => onDownload(attachment),
        onDelete: () => onDelete(attachment),
      );
}
