import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_timeframe.dart';

class RoadmapToolbar extends StatelessWidget {
  const RoadmapToolbar({
    super.key,
    required this.timeframe,
    required this.rangeLabel,
    required this.onTimeframeChanged,
    required this.onPrevious,
    required this.onNext,
    required this.onToday,
  });

  final RoadmapTimeframe timeframe;
  final String rangeLabel;
  final ValueChanged<RoadmapTimeframe> onTimeframeChanged;
  final VoidCallback onPrevious;
  final VoidCallback onNext;
  final VoidCallback onToday;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Wrap(
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: 8,
        children: [
          IconButton(
            icon: const Icon(Icons.chevron_left),
            tooltip: 'Previous',
            onPressed: onPrevious,
          ),
          IconButton(
            icon: const Icon(Icons.chevron_right),
            tooltip: 'Next',
            onPressed: onNext,
          ),
          TextButton(onPressed: onToday, child: const Text('Today')),
          Text(rangeLabel, style: Theme.of(context).textTheme.titleSmall),
          const SizedBox(width: 16),
          Text('Timeframe', style: Theme.of(context).textTheme.bodySmall),
          DropdownButton<RoadmapTimeframe>(
            value: timeframe,
            onChanged: (value) =>
                value == null ? null : onTimeframeChanged(value),
            items: [
              for (final option in RoadmapTimeframe.values)
                DropdownMenuItem(value: option, child: Text(option.label)),
            ],
          ),
        ],
      ),
    );
  }
}
