import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

void main() {
  group('AttachmentUpload.picked infers the content type', () {
    test('from the extension', () {
      final upload = AttachmentUpload.picked('report.pdf', const [1, 2, 3]);

      expect(upload.contentType, 'application/pdf');
    });

    test("from the file's opening bytes when there's no extension", () {
      const pngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

      final upload = AttachmentUpload.picked('screenshot', pngSignature);

      expect(upload.contentType, 'image/png');
    });

    test('as generic binary when neither says', () {
      final upload = AttachmentUpload.picked('data.weaverthing', const [1]);

      expect(upload.contentType, 'application/octet-stream');
    });
  });

  group('AttachmentUpload.photo', () {
    const jpegStart = [0xff, 0xd8, 0xff, 0xe0];

    test('names the photo after when it was taken, keeping the camera '
        "file's extension, instead of the camera's random name", () {
      final upload = AttachmentUpload.photo(
        'scaled_3f2a9c1e-77b0.jpg',
        jpegStart,
        takenAt: DateTime(2026, 11, 2, 9, 3, 7),
      );

      expect(upload.fileName, 'Photo 2026-11-02 09.03.07.jpg');
      expect(upload.contentType, 'image/jpeg');
      expect(upload.bytes, jpegStart);
    });

    test('falls back to .jpg when the camera file has no extension', () {
      final upload = AttachmentUpload.photo(
        'image',
        jpegStart,
        takenAt: DateTime(2026, 11, 2, 9, 3, 7),
      );

      expect(upload.fileName, 'Photo 2026-11-02 09.03.07.jpg');
      expect(upload.contentType, 'image/jpeg');
    });
  });
}
