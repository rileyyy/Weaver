import 'package:weaver/core/dates/calendar_days.dart';

/// Where an item's bar sits on the timeline, in days from the window start
/// (end exclusive), clipped to the window.
typedef RoadmapBarSpan = ({double startDay, double endDay});

/// The bar for an item scheduled [start]..[end] (both inclusive calendar
/// days) in a window of [totalDays] starting at [windowStart]. When only
/// one date is set, the bar runs off the window on the other side, as the
/// date window treats it as open there. Null when the item has no dates (a
/// group row, or a Backlog item) or falls entirely outside the window.
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

/// Where an item's bar sits on a timeline [RoadmapBarSpan] laid out in
/// pixels, and whether each edge is the item's own date rather than the
/// window's edge (a missing date, or one outside the window). Bars and the
/// dependency arrows both use it, so an arrow always meets its bar.
typedef RoadmapBarExtent = ({
  double left,
  double right,
  bool startsInWindow,
  bool endsInWindow,
});

/// [roadmapBarSpan] on a timeline [timelineWidth] pixels wide.
RoadmapBarExtent? roadmapBarExtent({
  required DateTime windowStart,
  required int totalDays,
  required double timelineWidth,
  required DateTime? start,
  required DateTime? end,
}) {
  final span = roadmapBarSpan(
    windowStart: windowStart,
    totalDays: totalDays,
    start: start,
    end: end,
  );
  if (span == null) return null;
  // Multiplying before dividing keeps whole-pixel days exact.
  double toPixels(double day) => day * timelineWidth / totalDays;
  // A drawn bar starts before the window's end and ends after its start,
  // so only the other side of each edge needs checking.
  return (
    left: toPixels(span.startDay),
    right: toPixels(span.endDay),
    startsInWindow: start != null && daysBetween(windowStart, start) >= 0,
    endsInWindow: end != null && daysBetween(windowStart, end) < totalDays,
  );
}
