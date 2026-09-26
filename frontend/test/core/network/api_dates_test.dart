import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_dates.dart';

void main() {
  test('formatCalendarDate sends the picked local day, whatever the time zone', () {
    expect(formatCalendarDate(DateTime(2026, 9, 25)), '2026-09-25');
    expect(formatCalendarDate(DateTime(2026, 9, 25, 23, 59)), '2026-09-25');
    expect(formatCalendarDate(null), isNull);
  });

  test('parseCalendarDate reads a date as local midnight on that day', () {
    final date = parseCalendarDate('2026-09-25')!;

    expect(date.isUtc, isFalse);
    expect([date.year, date.month, date.day, date.hour], [2026, 9, 25, 0]);
    expect(parseCalendarDate(null), isNull);
  });

  test('a calendar date survives a send and read round trip unchanged', () {
    final picked = DateTime(2026, 3, 29);

    expect(parseCalendarDate(formatCalendarDate(picked)), picked);
  });

  test('parseApiTimestamp converts a UTC instant to local time', () {
    final parsed = parseApiTimestamp('2026-09-24T22:00:00Z');

    expect(parsed.isUtc, isFalse);
    expect(parsed, DateTime.utc(2026, 9, 24, 22).toLocal());
  });
}
