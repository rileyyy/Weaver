import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/confirm_dialogs.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The files attached to this item: download one by tapping it, delete it
/// (after confirming), or attach another.
class AttachmentsSection extends StatelessWidget {
  const AttachmentsSection({
    super.key,
    required this.attachments,
    required this.isLoading,
    required this.loadError,
    required this.isUploading,
    required this.error,
    required this.onUpload,
    required this.onDownload,
    required this.onDelete,
  });

  final List<WorkItemAttachment> attachments;
  final bool isLoading;
  final String? loadError;
  final bool isUploading;

  /// Why the last upload, download or delete failed.
  final String? error;

  final VoidCallback onUpload;
  final ValueChanged<WorkItemAttachment> onDownload;

  /// Called only once the user has confirmed.
  final ValueChanged<WorkItemAttachment> onDelete;

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
      TextButton.icon(
        onPressed: isUploading ? null : onUpload,
        icon: const Icon(Icons.attach_file, size: 18),
        label: Text(isUploading ? 'Uploading…' : 'Attach file'),
      ),
    ];
  }

  Widget _tile(BuildContext context, WorkItemAttachment attachment) => ListTile(
    contentPadding: EdgeInsets.zero,
    dense: true,
    leading: const Icon(Icons.download, size: 18),
    title: Text(attachment.fileName, overflow: TextOverflow.ellipsis),
    subtitle: Text(attachment.displaySize),
    onTap: () => onDownload(attachment),
    trailing: IconButton(
      icon: const Icon(Icons.delete_outline, size: 18),
      tooltip: 'Delete attachment',
      onPressed: () => unawaited(_confirmDelete(context, attachment)),
    ),
  );

  Future<void> _confirmDelete(
    BuildContext context,
    WorkItemAttachment attachment,
  ) async {
    if (!await confirmDeleteAttachment(context, attachment.fileName)) return;
    onDelete(attachment);
  }
}
