import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/widgets/status_dot.dart';

class StatusFilterBar extends StatelessWidget {
  const StatusFilterBar({
    super.key,
    required this.statuses,
    required this.hiddenStatusIds,
    required this.onToggle,
  });

  final List<BoardStatus> statuses;
  final Set<String> hiddenStatusIds;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Show columns', style: Theme.of(context).textTheme.bodySmall),
        for (final status in statuses)
          FilterChip(
            avatar: StatusDot(color: status.color),
            label: Text(status.name),
            selected: !hiddenStatusIds.contains(status.id),
            onSelected: (_) => onToggle(status.id),
          ),
      ],
    );
  }
}
