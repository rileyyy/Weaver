import 'dart:typed_data';

import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

/// The device side of attachments: choosing a file to upload and handing a
/// downloaded one to the user. Behind an interface so view models can be
/// tested without the platform's file dialogs.
abstract class AttachmentFiles {
  /// Asks the user for one file; null if they cancel.
  Future<AttachmentUpload?> pick();

  /// Gives [bytes] to the user as [fileName]: a browser download on web, a
  /// save dialog elsewhere.
  Future<void> save(
    String fileName,
    Uint8List bytes, {
    required String contentType,
  });
}
