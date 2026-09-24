import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_grid.dart';

void main() {
  group('weeklyGridLineOffsets', () {
    test('places a line every 7 days regardless of timeline width', () {
      final offsets = weeklyGridLineOffsets(280, 28);

      // 280px / 28 days = 10px/day; lines at day 0, 7, 14, 21.
      expect(offsets, [0, 70, 140, 210]);
    });

    test('omits a trailing line that would fall exactly on the last day', () {
      final offsets = weeklyGridLineOffsets(70, 7);

      expect(offsets, [0]);
    });
  });

  group('todayLineOffset', () {
    final now = DateTime.now();
    final startOfToday = DateTime(now.year, now.month, now.day);

    test('returns an offset when today falls inside the visible window', () {
      final windowStart = startOfToday.subtract(const Duration(days: 3));

      final offset = todayLineOffset(
        windowStart: windowStart,
        timelineWidth: 300,
        totalDays: 30,
      );

      // 3 days into a 30-day window at 10px/day.
      expect(offset, 30);
    });

    test('returns null when today is before the visible window', () {
      final windowStart = startOfToday.add(const Duration(days: 1));

      final offset = todayLineOffset(
        windowStart: windowStart,
        timelineWidth: 300,
        totalDays: 30,
      );

      expect(offset, isNull);
    });

    test("returns null when today is on or after the window's end", () {
      final windowStart = startOfToday.subtract(const Duration(days: 30));

      final offset = todayLineOffset(
        windowStart: windowStart,
        timelineWidth: 300,
        totalDays: 30,
      );

      expect(offset, isNull);
    });
  });
}
