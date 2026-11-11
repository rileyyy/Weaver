import 'package:flutter/material.dart';
import 'package:weaver/features/board/views/agenda/widgets/status_menu_button.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// One Agenda item: its number and title, when it's on and, while several
/// Spaces show, which Space it's in, with its status to change in place.
class AgendaItemTile extends StatelessWidget {
  const AgendaItemTile({
    super.key,
    required this.item,
    required this.details,
    required this.statuses,
    required this.onOpen,
    required this.onStatusChanged,
  });

  final WorkItem item;

  /// The lines under the title, e.g. its range and Space; none may be
  /// empty.
  final List<String> details;
  final List<WorkItemStatus> statuses;
  final VoidCallback onOpen;
  final void Function(String statusId) onStatusChanged;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: const EdgeInsets.only(left: 16, right: 4),
      title: Text(
        '#${item.number} ${item.title}',
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: details.isEmpty
          ? null
          : Text(details.join(' · '), overflow: TextOverflow.ellipsis),
      onTap: onOpen,
      trailing: StatusMenuButton(
        statusId: item.statusId,
        statuses: statuses,
        onChanged: onStatusChanged,
      ),
    );
  }
}
