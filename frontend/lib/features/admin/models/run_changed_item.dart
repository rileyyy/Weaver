import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

/// A work item one run changed, with that run's changes to it, oldest first.
/// A purged item has no [number] any more, only the title history kept.
class RunChangedItem {
  const RunChangedItem({
    required this.title,
    required this.isDeleted,
    required this.changes,
    this.number,
  });

  factory RunChangedItem.fromJson(Map<String, dynamic> json) {
    final item = json['item'] as Map<String, dynamic>;
    return RunChangedItem(
      number: item['number'] as int?,
      title: item['title'] as String,
      isDeleted: json['deleted'] as bool,
      changes: [
        for (final change in json['changes'] as List<dynamic>)
          WorkItemHistoryEntry.fromJson(change as Map<String, dynamic>),
      ],
    );
  }

  final int? number;
  final String title;

  /// Deleted now: in the Trash or purged.
  final bool isDeleted;
  final List<WorkItemHistoryEntry> changes;

  /// "#12 Paint the hall", or just the title once the number is gone.
  String get name => number == null ? title : '#$number $title';
}
