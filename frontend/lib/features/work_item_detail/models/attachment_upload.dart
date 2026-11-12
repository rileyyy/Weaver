import 'package:mime/mime.dart';
import 'package:path/path.dart' as p;

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

  /// The camera names its files randomly, which tells the user nothing in
  /// the attachment list, so the photo is named after when it was taken.
  factory AttachmentUpload.photo(
    String cameraFileName,
    List<int> bytes, {
    required DateTime takenAt,
  }) {
    final extension = p.extension(cameraFileName);
    return AttachmentUpload.picked(
      'Photo ${_photoTimestamp(takenAt)}'
      '${extension.isEmpty ? '.jpg' : extension}',
      bytes,
    );
  }

  static const String genericBinary = 'application/octet-stream';

  // Dots, not colons, between the time parts: Windows forbids colons in
  // file names, and a downloaded photo keeps this name.
  static String _photoTimestamp(DateTime t) {
    String two(int n) => n.toString().padLeft(2, '0');
    return '${t.year}-${two(t.month)}-${two(t.day)} '
        '${two(t.hour)}.${two(t.minute)}.${two(t.second)}';
  }

  final String fileName;
  final List<int> bytes;
  final String contentType;
}
