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
}
