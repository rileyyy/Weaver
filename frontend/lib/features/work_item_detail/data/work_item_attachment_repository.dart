import 'dart:typed_data';

import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';

abstract class WorkItemAttachmentRepository {
  /// Oldest first, as the server sends them.
  Future<List<WorkItemAttachment>> loadAttachments(String workItemId);

  /// The server refuses a file over its size limit with a 413 whose message
  /// names the limit, and an empty file with a 400.
  Future<WorkItemAttachment> upload(String workItemId, AttachmentUpload file);

  /// The file's content, exactly as uploaded.
  Future<Uint8List> downloadContent(String attachmentId);

  /// Deletes the attachment and its file.
  Future<void> delete(String attachmentId);
}
