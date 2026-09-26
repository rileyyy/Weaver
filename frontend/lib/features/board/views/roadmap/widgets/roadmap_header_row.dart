import 'package:flutter/material.dart';
import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_view.dart';
import 'package:weaver/features/board/views/roadmap/widgets/roadmap_grid.dart';

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
    final totalDays = timeframe.totalDays;
    final outlineColor = Theme.of(context).colorScheme.outlineVariant;
    final gridOffsets = weeklyGridLineOffsets(timelineWidth, totalDays);
    final todayOffset = todayLineOffset(
      windowStart: windowStart,
      timelineWidth: timelineWidth,
      totalDays: totalDays,
    );

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
          child: Stack(
            children: [
              Row(
                children: [
                  for (var i = 0; i < timeframe.unitCount; i++)
                    SizedBox(
                      width: unitWidth,
                      child: Center(
                        child: Text(
                          timeframe.unitLabel(
                            addDays(windowStart, i * timeframe.daysPerUnit),
                          ),
                          style: Theme.of(context).textTheme.bodySmall,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ),
                ],
              ),
              for (final offset in gridOffsets)
                Positioned(
                  left: offset,
                  top: 0,
                  bottom: 0,
                  width: 1,
                  child: Container(color: outlineColor),
                ),
              if (todayOffset != null)
                Positioned(
                  left: todayOffset,
                  top: 0,
                  bottom: 0,
                  width: 2,
                  child: Container(color: roadmapTodayColor),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
