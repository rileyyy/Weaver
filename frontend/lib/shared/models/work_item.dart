import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';

/// One work item as the server sends it (`WorkItemDto`): what
/// `WorkItemStore` holds, and what every layout and the detail dialog read.
/// A new work-item field only needs adding here and in [WorkItem.fromJson].
class WorkItem {
  const WorkItem({
    required this.id,
    required this.number,
    required this.parentId,
    required this.title,
    required this.description,
    required this.statusId,
    required this.layerId,
    required this.priorityId,
    required this.assignedToUserId,
    required this.startDate,
    required this.endDate,
    required this.createdAt,
    required this.updatedAt,
    required this.version,
    this.tags = const [],
    this.recurrenceSourceId,
    this.recurrenceDate,
  });

  factory WorkItem.fromJson(Map<String, dynamic> json) => WorkItem(
    id: json['id'] as String,
    number: json['number'] as int,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    description: json['description'] as String?,
    statusId: json['statusId'] as String,
    layerId: json['layerId'] as String?,
    priorityId: json['priorityId'] as String?,
    assignedToUserId: json['assignedToUserId'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    updatedAt: parseApiTimestamp(json['updatedAtUtc'] as String),
    version: json['version'] as int,
    tags: stringList(json['tags']),
    recurrenceSourceId: json['recurrenceSourceId'] as String?,
    recurrenceDate: parseCalendarDate(json['recurrenceDate']),
  );

  final String id;

  /// The human-facing `#N`; see the glossary's **Number**.
  final int number;
  final String? parentId;
  final String title;
  final String? description;
  final String statusId;
  final String? layerId;
  final String? priorityId;
  final String? assignedToUserId;
  final DateTime? startDate;
  final DateTime? endDate;
  final DateTime createdAt;
  final DateTime updatedAt;

  /// Server-side row version. Sent back with overwriting saves so the
  /// backend can reject a save based on a stale read.
  final int version;
  final List<String> tags;

  /// The repeating item this one was created from, and the occurrence date
  /// it was created for; null for items created by hand.
  final String? recurrenceSourceId;
  final DateTime? recurrenceDate;
}
