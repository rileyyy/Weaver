import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_view.dart';

class RoadmapHeaderRow extends StatelessWidget {
  static const double _timelineHeaderHeight = 32;

  const RoadmapHeaderRow({
    super.key,
    required this.timeframe,
    required this.windowStart,
    required this.timelineWidth,
  });

  final RoadmapTimeframe timeframe;
  final DateTime windowStart;
  final double timelineWidth;

  @override
  Widget build(BuildContext context) {
    final unitWidth = timelineWidth / timeframe.unitCount;
    final outlineColor = Theme.of(context).colorScheme.outlineVariant;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: leftColumnWidth,
          child: Text(
            'Work Item',
            style: Theme.of(context).textTheme.labelLarge,
          ),
        ),
        SizedBox(
          width: timelineWidth,
          height: _timelineHeaderHeight,
          child: Row(
            children: [
              for (var i = 0; i < timeframe.unitCount; i++)
                Container(
                  width: unitWidth,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    border: Border(left: BorderSide(color: outlineColor)),
                  ),
                  child: Text(
                    timeframe.unitLabel(
                      windowStart.add(
                        Duration(days: i * timeframe.daysPerUnit),
                      ),
                    ),
                    style: Theme.of(context).textTheme.bodySmall,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
