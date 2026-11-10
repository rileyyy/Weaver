import 'dart:ui';

import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_bar.dart';

/// One blocks link drawn on the Roadmap: from the end of the blocker's bar
/// to the start of the blocked item's bar, in the timeline's coordinates
/// (x from the timeline's left edge, y from the top of the first row).
typedef RoadmapArrow = ({Offset from, Offset to, bool conflicts});

/// The arrows for every blocks link between two of [rows] (the visible
/// rows, top to bottom). A link is skipped when either end isn't drawn: the
/// blocker is collapsed or filtered out, or the blocker's end or the
/// blocked item's start is missing or outside the window, since an arrow
/// to a clipped bar edge would point at the wrong day.
List<RoadmapArrow> roadmapArrows({
  required List<HierarchyItem> rows,
  required DateTime windowStart,
  required int totalDays,
  required double timelineWidth,
  required double rowHeight,
}) {
  final rowIndexById = {
    for (final (index, item) in rows.indexed) item.id: index,
  };
  double rowCentre(int index) => (index + 0.5) * rowHeight;

  RoadmapBarExtent? barOf(HierarchyItem item) => roadmapBarExtent(
    windowStart: windowStart,
    totalDays: totalDays,
    timelineWidth: timelineWidth,
    start: item.startDate,
    end: item.endDate,
  );

  final arrows = <RoadmapArrow>[];
  for (final (blockedIndex, blocked) in rows.indexed) {
    final blockedBar = barOf(blocked);
    if (blockedBar == null || !blockedBar.startsInWindow) continue;
    for (final blocker in blocked.blockedBy) {
      final blockerIndex = rowIndexById[blocker.id];
      if (blockerIndex == null) continue;
      final blockerBar = barOf(rows[blockerIndex]);
      if (blockerBar == null || !blockerBar.endsInWindow) continue;
      arrows.add((
        from: Offset(blockerBar.right, rowCentre(blockerIndex)),
        to: Offset(blockedBar.left, rowCentre(blockedIndex)),
        conflicts: blocker.conflicts,
      ));
    }
  }
  return arrows;
}

/// How far an arrow runs straight out of the blocker's bar, and straight
/// into the blocked item's, before turning.
const double _stub = 8;

/// The corners of [arrow]'s line, made of horizontal and vertical runs and
/// always ending with a run rightwards into the blocked bar, so the
/// arrowhead points at the bar's start. When the blocked bar starts before
/// the blocker's end (a conflict, usually), the line doubles back along
/// the gap between rows rather than across either bar.
List<Offset> roadmapArrowRoute(
  RoadmapArrow arrow, {
  required double rowHeight,
}) {
  final from = arrow.from;
  final to = arrow.to;
  final outX = from.dx + _stub;
  final inX = to.dx - _stub;
  if (inX >= outX) {
    return [from, Offset(outX, from.dy), Offset(outX, to.dy), to];
  }
  final gapY = to.dy > from.dy ? to.dy - rowHeight / 2 : to.dy + rowHeight / 2;
  return [
    from,
    Offset(outX, from.dy),
    Offset(outX, gapY),
    Offset(inX, gapY),
    Offset(inX, to.dy),
    to,
  ];
}
