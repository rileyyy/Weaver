import 'package:mime/mime.dart';
import 'package:path/path.dart' as p;

const String _genericBinary = 'application/octet-stream';
const String _genericExtension = 'bin';

/// The name to save a downloaded attachment under. file_picker's web save
/// throws for a name with no extension (e.g. "Makefile"), using this same
/// `p.extension` check, so such a name gets one from its content type.
String downloadFileName(String fileName, String contentType) {
  if (p.extension(fileName).isNotEmpty) return fileName;
  final extension = contentType == _genericBinary
      ? _genericExtension
      : extensionFromMime(contentType) ?? _genericExtension;
  return '$fileName.$extension';
}
