import 'package:mime/mime.dart';
import 'package:path/path.dart' as p;
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

const String _genericExtension = 'bin';

/// The name to save a downloaded attachment under. file_picker's web save
/// throws for a name with no extension (e.g. "Makefile"), using this same
/// `p.extension` check, so such a name gets one from its content type.
String downloadFileName(String fileName, String contentType) {
  if (p.extension(fileName).isNotEmpty) return fileName;
  final extension = contentType == AttachmentUpload.genericBinary
      ? _genericExtension
      : extensionFromMime(contentType) ?? _genericExtension;
  return '$fileName.$extension';
}
