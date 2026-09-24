import 'package:flutter/material.dart';

/// Vertical gridlines are always spaced by calendar week, independent of
/// [RoadmapTimeframe]'s own column width (a day for week/fortnight/month, a
/// week for quarter, a month for year) — otherwise week/fortnight/month
/// would draw one divider per day, which is far busier than the timeline
/// needs.
const int roadmapGridIntervalDays = 7;

/// The pumpkin "today" marker color, shared by the header and every row so
/// the line reads as one continuous marker down the timeline.
const Color roadmapTodayColor = Color(0xFFFF7518);

/// X-offsets (from the timeline's left edge) of each weekly gridline.
List<double> weeklyGridLineOffsets(double timelineWidth, int totalDays) {
  final dayWidth = timelineWidth / totalDays;
  return [
    for (var day = 0; day < totalDays; day += roadmapGridIntervalDays)
      day * dayWidth,
  ];
}

/// X-offset of the "today" marker, or null if today falls outside the
/// visible [windowStart, windowStart + totalDays) window.
double? todayLineOffset({
  required DateTime windowStart,
  required double timelineWidth,
  required int totalDays,
}) {
  final now = DateTime.now();
  final startOfToday = DateTime(now.year, now.month, now.day);
  final offsetDays = startOfToday.difference(windowStart).inDays;
  if (offsetDays < 0 || offsetDays >= totalDays) return null;

  final dayWidth = timelineWidth / totalDays;
  return offsetDays * dayWidth;
}
