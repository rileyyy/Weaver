import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/download_file_name.dart';

void main() {
  group('downloadFileName', () {
    test('keeps a name that already has an extension', () {
      expect(downloadFileName('report.pdf', 'application/pdf'), 'report.pdf');
    });

    test('adds the extension of a known content type', () {
      expect(downloadFileName('scan', 'image/png'), 'scan.png');
    });

    test('adds .bin for generic binary content', () {
      expect(
        downloadFileName('Makefile', 'application/octet-stream'),
        'Makefile.bin',
      );
    });

    test('adds .bin for a content type with no known extension', () {
      expect(
        downloadFileName('blob', 'application/x-weaver-unknown'),
        'blob.bin',
      );
    });
  });
}
