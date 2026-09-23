import 'package:flutter/material.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/heirarchy/heirarchy_column.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/status_dot.dart';
import 'package:weaver/features/board/widgets/tag_badge.dart';

class ColumnValue extends StatelessWidget {
  const ColumnValue({
    super.key,
    required this.column,
    required this.item,
    required this.viewModel,
    required this.onAssignTapped,
  });

  final HierarchyColumn column;
  final HierarchyItem item;
  final BoardViewModel viewModel;
  final VoidCallback onAssignTapped;

  @override
  Widget build(BuildContext context) {
    return switch (column) {
      HierarchyColumn.status => Row(
        children: [
          StatusDot(color: viewModel.statusColorFor(item.statusId)),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              viewModel.statusNameFor(item.statusId) ?? '',
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        ],
      ),
      HierarchyColumn.assignedTo => Row(
        children: [
          AssigneeAvatar(
            initial: viewModel.assigneeInitialFor(item.assignedToUserId),
            showPlaceholderWhenUnassigned: true,
            onTap: onAssignTapped,
          ),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              viewModel.usernameFor(item.assignedToUserId) ?? 'Unassigned',
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
        ],
      ),
      HierarchyColumn.tags => TagBadgeRow(tags: item.tags),
    };
  }
}
