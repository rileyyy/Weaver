import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/status_dot.dart';
import 'package:weaver/shared/models/work_item_status.dart';

class StatusFilterBar extends StatelessWidget {
  const StatusFilterBar({
    super.key,
    required this.statuses,
    required this.hiddenStatusIds,
    required this.onToggle,
    this.spaceNameOf,
  });

  final List<WorkItemStatus> statuses;
  final Set<String> hiddenStatusIds;
  final ValueChanged<String> onToggle;

  /// The Space a status belongs to, to tell apart same-named statuses
  /// while several Spaces are shown; null while one is.
  final String? Function(String statusId)? spaceNameOf;

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
            label: Text(switch (spaceNameOf?.call(status.id)) {
              final space? => '${status.name} ($space)',
              null => status.name,
            }),
            selected: !hiddenStatusIds.contains(status.id),
            onSelected: (_) => onToggle(status.id),
          ),
      ],
    );
  }
}
