import 'dart:typed_data';

import 'package:weaver/features/work_item_detail/data/attachment_files.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

/// Stands in for the platform's file dialogs: [picked] is what the user
/// chooses (null cancels), and saves are recorded.
class FakeAttachmentFiles implements AttachmentFiles {
  FakeAttachmentFiles({this.picked});

  AttachmentUpload? picked;
  final List<(String, Uint8List, String)> saved = [];

  @override
  Future<AttachmentUpload?> pick() async => picked;

  @override
  Future<void> save(
    String fileName,
    Uint8List bytes, {
    required String contentType,
  }) async => saved.add((fileName, bytes, contentType));
}
