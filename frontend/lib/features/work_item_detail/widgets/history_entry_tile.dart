import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';
import 'package:weaver/shared/widgets/user_state_badge.dart';

/// One History tab row: who (with an agent badge and the label it sent),
/// when, and the change.
class HistoryEntryTile extends StatelessWidget {
  const HistoryEntryTile({super.key, required this.entry});

  final WorkItemHistoryEntry entry;

  @override
  Widget build(BuildContext context) {
    final small = Theme.of(context).textTheme.bodySmall;
    final detail = entry.actorDetail;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
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
          const SizedBox(height: 4),
          Text(entry.change),
        ],
      ),
    );
  }
}
