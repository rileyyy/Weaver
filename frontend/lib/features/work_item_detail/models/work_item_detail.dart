import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

/// The full record for one work item — a superset of `WorkItemCard`'s
/// board-display fields, used by the detail screen rather than the board.
class WorkItemDetail {
  const WorkItemDetail({
    required this.id,
    required this.parentId,
    required this.title,
    required this.description,
    required this.statusId,
    required this.layerId,
    required this.priority,
    required this.assignedToUserId,
    required this.startDate,
    required this.endDate,
    required this.createdAtUtc,
    required this.updatedAtUtc,
    required this.version,
    this.tags = const [],
  });

  final String id;
  final String? parentId;
  final String title;
  final String? description;
  final String statusId;
  final String? layerId;
  final WorkItemPriority priority;
  final String? assignedToUserId;
  final DateTime? startDate;
  final DateTime? endDate;
  final DateTime createdAtUtc;
  final DateTime updatedAtUtc;

  /// Server-side row version. Sent back with overwriting saves so the
  /// backend can reject a save based on a stale read.
  final int version;
  final List<String> tags;
}
