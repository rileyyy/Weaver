import 'package:weaver/core/dates/calendar_days.dart';

/// Where an item's bar sits on the timeline, in days from the window start
/// (end exclusive), clipped to the window.
typedef RoadmapBarSpan = ({double startDay, double endDay});

/// The bar for an item scheduled [start]..[end] (both inclusive calendar
/// days) in a window of [totalDays] starting at [windowStart]. A missing
/// date runs off that edge of the window, the same open-ended convention
/// as the board's time filter. Null when the item has no dates or falls
/// entirely outside the window.
RoadmapBarSpan? roadmapBarSpan({
  required DateTime windowStart,
  required int totalDays,
  required DateTime? start,
  required DateTime? end,
}) {
  if (start == null && end == null) return null;

  final windowDays = totalDays.toDouble();
  final rawStart = start == null
      ? 0.0
      : daysBetween(windowStart, start).toDouble();
  // +1: the end date is inclusive, so a one-day item still has a
  // one-day-wide bar.
  final rawEnd = end == null
      ? windowDays
      : daysBetween(windowStart, end).toDouble() + 1;

  final startDay = rawStart.clamp(0.0, windowDays);
  final endDay = rawEnd.clamp(0.0, windowDays);
  if (endDay <= startDay) return null;
  return (startDay: startDay, endDay: endDay);
}
