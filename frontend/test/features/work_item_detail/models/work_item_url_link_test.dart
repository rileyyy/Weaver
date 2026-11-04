import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';

void main() {
  group('fromJson', () {
    test('parses the URL and label', () {
      final link = WorkItemUrlLink.fromJson({
        'id': 'url-1',
        'url': 'https://github.com/o/r/pull/7',
        'label': 'PR #7',
      });

      expect(link.id, 'url-1');
      expect(link.url, 'https://github.com/o/r/pull/7');
      expect(link.label, 'PR #7');
      expect(link.displayName, 'PR #7');
    });

    test('shows the URL when there is no label', () {
      final link = WorkItemUrlLink.fromJson({
        'id': 'url-1',
        'url': 'https://example.com/quote',
        'label': null,
      });

      expect(link.label, isNull);
      expect(link.displayName, 'https://example.com/quote');
    });
  });

  group('urlError', () {
    test('accepts absolute http and https URLs', () {
      expect(WorkItemUrlLink.urlError('https://example.com/a?b=1'), isNull);
      expect(WorkItemUrlLink.urlError('http://example.com'), isNull);
    });

    test('rejects a blank URL', () {
      expect(WorkItemUrlLink.urlError('  '), isNotNull);
    });

    test('rejects a relative URL or one without a host', () {
      expect(WorkItemUrlLink.urlError('example.com/page'), isNotNull);
      expect(WorkItemUrlLink.urlError('https://'), isNotNull);
    });

    test('rejects other schemes', () {
      expect(WorkItemUrlLink.urlError('ftp://example.com/file'), isNotNull);
      expect(WorkItemUrlLink.urlError('javascript:alert(1)'), isNotNull);
    });
  });
}
