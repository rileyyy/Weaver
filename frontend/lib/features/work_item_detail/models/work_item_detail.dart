import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';
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
    required this.createdAt,
    required this.updatedAt,
    required this.version,
    this.tags = const [],
  });

  factory WorkItemDetail.fromJson(Map<String, dynamic> json) => WorkItemDetail(
    id: json['id'] as String,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    description: json['description'] as String?,
    statusId: json['statusId'] as String,
    layerId: json['layerId'] as String?,
    priority: workItemPriorityFromWire(json['priority'] as String),
    assignedToUserId: json['assignedToUserId'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    updatedAt: parseApiTimestamp(json['updatedAtUtc'] as String),
    version: json['version'] as int,
    tags: stringList(json['tags']),
  );

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
  final DateTime createdAt;
  final DateTime updatedAt;

  /// Server-side row version. Sent back with overwriting saves so the
  /// backend can reject a save based on a stale read.
  final int version;
  final List<String> tags;
}
