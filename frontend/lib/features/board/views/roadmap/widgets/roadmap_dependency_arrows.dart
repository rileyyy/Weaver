import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_arrows.dart';

/// Paints the Roadmap's blocks links over the timeline. It sits over the
/// rows' list rather than inside it, so it follows [scroll] itself: the
/// rows are built lazily and no single row could draw a link spanning
/// several of them.
class RoadmapDependencyArrowsPainter extends CustomPainter {
  RoadmapDependencyArrowsPainter({
    required this.arrows,
    required this.rowHeight,
    required this.scroll,
    required this.neutralColor,
    required this.conflictColor,
  }) : super(repaint: scroll);

  static const double _headLength = 6;
  static const double _headHalfWidth = 3.5;

  final List<RoadmapArrow> arrows;
  final double rowHeight;
  final ScrollController scroll;
  final Color neutralColor;
  final Color conflictColor;

  @override
  void paint(Canvas canvas, Size size) {
    final scrollOffset = scroll.hasClients ? scroll.offset : 0.0;
    canvas
      ..save()
      ..clipRect(Offset.zero & size)
      ..translate(0, -scrollOffset);
    // Conflicts last, so a red line is never hidden under a neutral one.
    for (final arrow in [
      for (final arrow in arrows)
        if (!arrow.conflicts) arrow,
      for (final arrow in arrows)
        if (arrow.conflicts) arrow,
    ]) {
      _paintArrow(canvas, arrow);
    }
    canvas.restore();
  }

  void _paintArrow(Canvas canvas, RoadmapArrow arrow) {
    final color = arrow.conflicts ? conflictColor : neutralColor;
    final route = roadmapArrowRoute(arrow, rowHeight: rowHeight);
    final tip = route.last;
    final line = Path()..moveTo(route.first.dx, route.first.dy);
    for (final corner in route.skip(1)) {
      line.lineTo(corner.dx, corner.dy);
    }
    canvas.drawPath(
      line,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = arrow.conflicts ? 2 : 1.5,
    );
    // The route always ends running rightwards, so the head points right.
    final head = Path()
      ..moveTo(tip.dx, tip.dy)
      ..lineTo(tip.dx - _headLength, tip.dy - _headHalfWidth)
      ..lineTo(tip.dx - _headLength, tip.dy + _headHalfWidth)
      ..close();
    canvas.drawPath(head, Paint()..color = color);
  }

  @override
  bool shouldRepaint(RoadmapDependencyArrowsPainter oldDelegate) =>
      // By value: the view builds a new list on every build, and the
      // arrows are records, so equal lists mean nothing moved.
      !listEquals(oldDelegate.arrows, arrows) ||
      oldDelegate.rowHeight != rowHeight ||
      oldDelegate.scroll != scroll ||
      oldDelegate.neutralColor != neutralColor ||
      oldDelegate.conflictColor != conflictColor;
}
