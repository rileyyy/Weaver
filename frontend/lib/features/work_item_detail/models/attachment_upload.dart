import 'package:mime/mime.dart';

/// A file the user picked to attach, read into memory: web, the production
/// target, can't hand over a path to stream from.
class AttachmentUpload {
  const AttachmentUpload({
    required this.fileName,
    required this.bytes,
    required this.contentType,
  });

  /// The file pickers don't report a content type on every platform (web's
  /// doesn't), and the server serves the file back with the one sent here,
  /// so it's inferred from the name, then the opening bytes.
  factory AttachmentUpload.picked(String fileName, List<int> bytes) =>
      AttachmentUpload(
        fileName: fileName,
        bytes: bytes,
        contentType:
            lookupMimeType(fileName, headerBytes: bytes) ?? genericBinary,
      );

  static const String genericBinary = 'application/octet-stream';

  final String fileName;
  final List<int> bytes;
  final String contentType;
}
