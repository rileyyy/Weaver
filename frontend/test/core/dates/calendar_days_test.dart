import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/dates/calendar_days.dart';

// 2026 DST changes: Europe springs forward on 29 March and falls back on
// 25 October; the US on 8 March and 1 November. Run the suite under a
// zone that observes DST (e.g. TZ=Europe/Stockholm) to exercise them.
void main() {
  test('addDays lands on local midnight across a spring-forward change', () {
    expect(addDays(DateTime(2026, 3, 28), 2), DateTime(2026, 3, 30));
    expect(addDays(DateTime(2026, 3, 7), 2), DateTime(2026, 3, 9));
  });

  test('addDays lands on local midnight across a fall-back change', () {
    expect(addDays(DateTime(2026, 10, 24), 2), DateTime(2026, 10, 26));
    expect(addDays(DateTime(2026, 11, 1), -1), DateTime(2026, 10, 31));
  });

  test('daysBetween counts calendar days across DST changes', () {
    expect(daysBetween(DateTime(2026, 3, 28), DateTime(2026, 3, 30)), 2);
    expect(daysBetween(DateTime(2026, 10, 24), DateTime(2026, 10, 26)), 2);
    expect(daysBetween(DateTime(2026, 3, 30), DateTime(2026, 3, 28)), -2);
  });

  test('daysBetween ignores the time of day', () {
    expect(
      daysBetween(DateTime(2026, 9, 25, 23, 59), DateTime(2026, 9, 26)),
      1,
    );
    expect(daysBetween(DateTime(2026, 9, 25), DateTime(2026, 9, 25, 18)), 0);
  });

  test('dateOnly drops the time of day', () {
    expect(dateOnly(DateTime(2026, 9, 25, 14, 30)), DateTime(2026, 9, 25));
  });
}
