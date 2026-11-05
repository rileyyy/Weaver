import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';

WorkItemAttachment _sized(int sizeBytes) => WorkItemAttachment(
  id: 'att-1',
  workItemId: 'item-1',
  fileName: 'report.pdf',
  contentType: 'application/pdf',
  sizeBytes: sizeBytes,
  createdAt: DateTime(2026, 11, 1),
);

void main() {
  test('displaySize shows bytes, KB, MB and GB in steps of 1024', () {
    expect(_sized(0).displaySize, '0 B');
    expect(_sized(1023).displaySize, '1023 B');
    expect(_sized(1024).displaySize, '1 KB');
    expect(_sized(1536).displaySize, '1.5 KB');
    expect(_sized(10650).displaySize, '10 KB');
    expect(_sized(25 * 1024 * 1024).displaySize, '25 MB');
    expect(_sized(3 * 1024 * 1024 * 1024).displaySize, '3 GB');
  });
}
