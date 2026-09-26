import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/dates/date_format.dart';

void main() {
  test('formats as zero-padded YYYY-MM-DD', () {
    expect(formatDate(DateTime(2026, 3, 7)), '2026-03-07');
  });

  test('uses the calendar fields as given, ignoring the time of day', () {
    expect(formatDate(DateTime(2026, 12, 31, 23, 59)), '2026-12-31');
  });
}
