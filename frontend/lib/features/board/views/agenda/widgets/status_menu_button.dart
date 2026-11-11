import 'package:flutter/material.dart';
import 'package:weaver/features/board/widgets/status_dot.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// An item's status, which opens a menu of its Space's [statuses] to pick
/// another from without opening the item.
class StatusMenuButton extends StatelessWidget {
  const StatusMenuButton({
    super.key,
    required this.statusId,
    required this.statuses,
    required this.onChanged,
  });

  final String statusId;
  final List<WorkItemStatus> statuses;
  final void Function(String statusId) onChanged;

  // Long status names are cut short so the title keeps most of a phone's
  // width.
  static const double _maxLabelWidth = 96;

  @override
  Widget build(BuildContext context) {
    final current = statuses.where((s) => s.id == statusId).firstOrNull;
    return PopupMenuButton<String>(
      tooltip: 'Change status',
      initialValue: statusId,
      onSelected: (id) {
        if (id != statusId) onChanged(id);
      },
      itemBuilder: (context) => [
        for (final status in statuses)
          PopupMenuItem(
            value: status.id,
            child: Row(
              spacing: 8,
              children: [
                StatusDot(color: status.color),
                Text(status.name),
              ],
            ),
          ),
      ],
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 12),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          spacing: 6,
          children: [
            StatusDot(color: current?.color),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: _maxLabelWidth),
              child: Text(
                current?.name ?? 'Status',
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelMedium,
              ),
            ),
            const Icon(Icons.arrow_drop_down, size: 18),
          ],
        ),
      ),
    );
  }
}
