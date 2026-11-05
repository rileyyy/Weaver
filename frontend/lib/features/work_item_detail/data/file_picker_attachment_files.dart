import 'dart:typed_data';

import 'package:file_picker/file_picker.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/features/work_item_detail/data/attachment_files.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/download_file_name.dart';

@LazySingleton(as: AttachmentFiles)
class FilePickerAttachmentFiles implements AttachmentFiles {
  @override
  Future<AttachmentUpload?> pick() async {
    final file = await FilePicker.pickFile();
    if (file == null) return null;
    return AttachmentUpload.picked(file.name, await file.readAsBytes());
  }

  @override
  Future<void> save(
    String fileName,
    Uint8List bytes, {
    required String contentType,
  }) async {
    // On web this is a Blob download through an anchor, which needs no
    // token: the bytes were already fetched with one.
    await FilePicker.saveFile(
      fileName: downloadFileName(fileName, contentType),
      bytes: bytes,
      mimeType: contentType,
    );
  }
}
