import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/admin/models/space_history_entry.dart';
import 'package:weaver/shared/widgets/user_state_badge.dart';

/// One row of a Space's history: the change, then who made it and when.
class SpaceHistoryEntryTile extends StatelessWidget {
  const SpaceHistoryEntryTile({super.key, required this.entry});

  final SpaceHistoryEntry entry;

  @override
  Widget build(BuildContext context) {
    final small = Theme.of(context).textTheme.bodySmall;
    final detail = entry.actorDetail;
    return ListTile(
      title: Text(entry.change),
      subtitle: Wrap(
        spacing: 8,
        runSpacing: 4,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          Text(
            entry.actorName,
            style: small?.copyWith(fontWeight: FontWeight.w600),
          ),
          if (entry.isAgent) const UserStateBadge(label: 'Agent'),
          if (detail != null) Text(detail, style: small),
          Text(
            '${formatDate(entry.changedAt)} ${formatTime(entry.changedAt)}',
            style: small,
          ),
        ],
      ),
    );
  }
}
