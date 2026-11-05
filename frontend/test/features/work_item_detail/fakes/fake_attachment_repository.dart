import 'dart:typed_data';

import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_attachment_repository.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';

WorkItemAttachment attachment({
  String id = 'att-1',
  String fileName = 'report.pdf',
  String contentType = 'application/pdf',
  int sizeBytes = 2048,
}) => WorkItemAttachment(
  id: id,
  workItemId: 'item-1',
  fileName: fileName,
  contentType: contentType,
  sizeBytes: sizeBytes,
  createdAt: DateTime(2026, 11, 1),
);

/// Keeps attachments and their content in memory, like the server would.
class FakeAttachmentRepository implements WorkItemAttachmentRepository {
  FakeAttachmentRepository([List<WorkItemAttachment> attachments = const []])
    : attachments = [...attachments];

  final List<WorkItemAttachment> attachments;
  final Map<String, Uint8List> contents = {};
  final List<AttachmentUpload> uploads = [];
  final List<String> deleted = [];

  /// When set, the next call fails with it.
  ApiException? failure;

  void _failIfSet() {
    final error = failure;
    if (error == null) return;
    failure = null;
    throw error;
  }

  @override
  Future<List<WorkItemAttachment>> loadAttachments(String workItemId) async {
    _failIfSet();
    return [...attachments];
  }

  @override
  Future<WorkItemAttachment> upload(
    String workItemId,
    AttachmentUpload file,
  ) async {
    _failIfSet();
    uploads.add(file);
    final created = attachment(
      id: 'att-${attachments.length + 1}',
      fileName: file.fileName,
      contentType: file.contentType,
      sizeBytes: file.bytes.length,
    );
    attachments.add(created);
    return created;
  }

  @override
  Future<Uint8List> downloadContent(String attachmentId) async {
    _failIfSet();
    return contents[attachmentId] ?? Uint8List(0);
  }

  @override
  Future<void> delete(String attachmentId) async {
    _failIfSet();
    deleted.add(attachmentId);
    attachments.removeWhere((a) => a.id == attachmentId);
  }
}
