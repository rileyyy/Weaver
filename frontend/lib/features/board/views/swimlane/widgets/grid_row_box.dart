import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';

/// One row of the swim-lane grid (a status header row or a swimlane row),
/// giving it the fixed [height] every row shares and, when [showBottomBorder]
/// is set, the horizontal grid line that separates it from the row below —
/// shared by both the pinned label column and the scrollable columns area so
/// the lines stay aligned across both.
class GridRowBox extends StatelessWidget {
  const GridRowBox({
    super.key,
    required this.height,
    required this.showBottomBorder,
    this.child,
  });

  final double height;
  final bool showBottomBorder;
  final Widget? child;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      decoration: showBottomBorder
          ? const BoxDecoration(
              border: Border(
                bottom: BorderSide(color: SwimlaneView.gridLineColor),
              ),
            )
          : null,
      child: child,
    );
  }
}
