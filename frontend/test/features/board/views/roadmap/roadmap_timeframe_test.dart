import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';

void main() {
  test('each timeframe spans the expected number of days', () {
    expect(
      {for (final t in RoadmapTimeframe.values) t: t.totalDays},
      {
        RoadmapTimeframe.week: 7,
        RoadmapTimeframe.fortnight: 14,
        RoadmapTimeframe.month: 30,
        RoadmapTimeframe.quarter: 91,
        RoadmapTimeframe.year: 360,
      },
    );
  });

  test('day-granular timeframes label columns by day of month', () {
    final date = DateTime(2026, 9, 5);
    for (final timeframe in [
      RoadmapTimeframe.week,
      RoadmapTimeframe.fortnight,
      RoadmapTimeframe.month,
    ]) {
      expect(timeframe.unitLabel(date), '5');
    }
  });

  test('quarter labels weeks by month and day, year labels months', () {
    expect(RoadmapTimeframe.quarter.unitLabel(DateTime(2026, 1, 12)), 'Jan 12');
    expect(RoadmapTimeframe.year.unitLabel(DateTime(2026, 12, 1)), 'Dec');
  });
}
