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

Map<String, dynamic> _json({bool? hasThumbnail}) => {
  'id': 'att-1',
  'workItemId': 'item-1',
  'fileName': 'photo.jpg',
  'contentType': 'image/jpeg',
  'sizeBytes': 2048,
  'createdAtUtc': '2026-11-01T09:30:00Z',
  'hasThumbnail': ?hasThumbnail,
};

void main() {
  test('fromJson reads whether the server has a thumbnail', () {
    expect(
      WorkItemAttachment.fromJson(_json(hasThumbnail: true)).hasThumbnail,
      isTrue,
    );
    expect(
      WorkItemAttachment.fromJson(_json(hasThumbnail: false)).hasThumbnail,
      isFalse,
    );
  });

  test('fromJson treats a missing hasThumbnail as no thumbnail, as servers '
      'from before thumbnails send', () {
    expect(WorkItemAttachment.fromJson(_json()).hasThumbnail, isFalse);
  });

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
