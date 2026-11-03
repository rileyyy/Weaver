import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';

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
    required this.priorityId,
    required this.assignedToUserId,
    required this.startDate,
    required this.endDate,
    required this.createdAt,
    required this.updatedAt,
    required this.version,
    this.tags = const [],
    this.when,
    this.whenResolved = false,
    this.recurrenceSourceId,
    this.recurrenceDate,
  });

  factory WorkItemDetail.fromJson(Map<String, dynamic> json) => WorkItemDetail(
    id: json['id'] as String,
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
    when: json['when'] as String?,
    whenResolved: json['whenResolved'] as bool? ?? false,
    recurrenceSourceId: json['recurrenceSourceId'] as String?,
    recurrenceDate: parseCalendarDate(json['recurrenceDate']),
  );

  final String id;
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

  /// The free-text phrase behind the item's dates ("after drywall"), and
  /// whether the server counts it as resolved (backed by dates). Weaver
  /// itself never turns the phrase into dates; agents and quick picks do.
  final String? when;
  final bool whenResolved;

  /// The repeating item this one was created from, and the occurrence date
  /// it was created for; null for items created by hand.
  final String? recurrenceSourceId;
  final DateTime? recurrenceDate;
}
