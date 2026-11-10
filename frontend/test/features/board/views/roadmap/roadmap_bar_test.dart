import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_bar.dart';

void main() {
  final windowStart = DateTime(2026, 3, 23);

  RoadmapBarSpan? span(DateTime? start, DateTime? end) => roadmapBarSpan(
    windowStart: windowStart,
    totalDays: 14,
    start: start,
    end: end,
  );

  test('a one-day item is one day wide', () {
    expect(span(DateTime(2026, 3, 25), DateTime(2026, 3, 25)), (
      startDay: 2.0,
      endDay: 3.0,
    ));
  });

  test('an item spanning a DST change keeps its full length', () {
    // 28 March to 31 March inclusive is four days, even though the
    // European clocks change on the 29th.
    expect(span(DateTime(2026, 3, 28), DateTime(2026, 3, 31)), (
      startDay: 5.0,
      endDay: 9.0,
    ));
  });

  test('missing dates run off the edge of the window', () {
    expect(span(null, DateTime(2026, 3, 25)), (startDay: 0.0, endDay: 3.0));
    expect(span(DateTime(2026, 3, 30), null), (startDay: 7.0, endDay: 14.0));
  });

  test('items outside the window, or with no dates, get no bar', () {
    expect(span(DateTime(2026, 1, 1), DateTime(2026, 1, 5)), isNull);
    expect(span(DateTime(2026, 5, 1), null), isNull);
    expect(span(null, null), isNull);
  });

  group('roadmapBarExtent', () {
    // 14 days over 140px: 10px a day.
    RoadmapBarExtent? extent(DateTime? start, DateTime? end) =>
        roadmapBarExtent(
          windowStart: windowStart,
          totalDays: 14,
          timelineWidth: 140,
          start: start,
          end: end,
        );

    test('lays the span out in pixels, both edges in the window', () {
      expect(extent(DateTime(2026, 3, 25), DateTime(2026, 3, 27)), (
        left: 20.0,
        right: 50.0,
        startsInWindow: true,
        endsInWindow: true,
      ));
    });

    test('an item on the first and last days of the window has both edges '
        'in it', () {
      expect(extent(DateTime(2026, 3, 23), DateTime(2026, 4, 5)), (
        left: 0.0,
        right: 140.0,
        startsInWindow: true,
        endsInWindow: true,
      ));
    });

    test('an edge cut off by the window or a missing date is not in it', () {
      expect(extent(DateTime(2026, 3, 1), DateTime(2026, 5, 1)), (
        left: 0.0,
        right: 140.0,
        startsInWindow: false,
        endsInWindow: false,
      ));
      expect(extent(null, null), isNull);
      expect(extent(null, DateTime(2026, 3, 25))?.startsInWindow, isFalse);
      expect(extent(DateTime(2026, 3, 25), null)?.endsInWindow, isFalse);
    });

    test('is null when there is no bar', () {
      expect(extent(DateTime(2026, 1, 1), DateTime(2026, 1, 5)), isNull);
    });
  });
}
