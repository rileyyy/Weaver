import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';

/// One work item as the server sends it (`WorkItemDto`): what
/// `WorkItemStore` holds, and what every layout and the detail dialog read.
/// A new work-item field only needs adding here, in [WorkItem.fromJson] and
/// in `_copy`.
class WorkItem {
  const WorkItem({
    required this.id,
    required this.number,
    required this.spaceId,
    required this.parentId,
    required this.title,
    required this.description,
    required this.statusId,
    required this.typeId,
    required this.priorityId,
    required this.assignedToUserId,
    required this.startDate,
    required this.endDate,
    required this.createdAt,
    required this.updatedAt,
    required this.version,
    this.tags = const [],
    this.fields = const {},
    this.when,
    this.whenResolved = false,
    this.recurrenceSourceId,
    this.recurrenceDate,
  });

  factory WorkItem.fromJson(Map<String, dynamic> json) => WorkItem(
    id: json['id'] as String,
    number: json['number'] as int,
    spaceId: json['spaceId'] as String,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    description: json['description'] as String?,
    statusId: json['statusId'] as String,
    typeId: json['typeId'] as String?,
    priorityId: json['priorityId'] as String?,
    assignedToUserId: json['assignedToUserId'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
    updatedAt: parseApiTimestamp(json['updatedAtUtc'] as String),
    version: json['version'] as int,
    tags: stringList(json['tags']),
    fields: (json['fields'] as Map<String, dynamic>?) ?? const {},
    when: json['when'] as String?,
    whenResolved: json['whenResolved'] as bool? ?? false,
    recurrenceSourceId: json['recurrenceSourceId'] as String?,
    recurrenceDate: parseCalendarDate(json['recurrenceDate']),
  );

  final String id;

  /// The human-facing `#N`; see the glossary's **Number**.
  final int number;

  /// The Space the item belongs to. Children share their parent's, and a
  /// reparent never changes it.
  final String spaceId;
  final String? parentId;
  final String title;
  final String? description;
  final String statusId;
  final String? typeId;
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

  /// The values of the item's Type's custom fields, by field id, as the
  /// server sends them (a string, number, bool or list); a field with no
  /// value is absent. Changing the Type clears them on the server.
  final Map<String, Object?> fields;

  /// The free-text phrase behind the item's dates ("after drywall"), and
  /// whether the server counts it as resolved (backed by dates). Weaver
  /// itself never turns the phrase into dates; agents and quick picks do.
  final String? when;
  final bool whenResolved;

  /// The repeating item this one was created from, and the occurrence date
  /// it was created for; null for items created by hand.
  final String? recurrenceSourceId;
  final DateTime? recurrenceDate;

  // The edits a layout can make before the server confirms them, each the
  // model-level mirror of one backend operation. A status change never
  // touches the parent and a reparent never touches the status, as on the
  // server.

  WorkItem withStatus(String statusId) => _copy(statusId: statusId);

  WorkItem movedToParent(String? parentId) => _copy(parentId: parentId);

  WorkItem rescheduled(DateTime? startDate, DateTime? endDate) =>
      _copy(startDate: startDate, endDate: endDate);

  WorkItem assigned(String? userId) => _copy(assignedToUserId: userId);

  WorkItem tagged(List<String> tags) => _copy(tags: tags);

  static const Object _unchanged = Object();

  // Nullable fields default to [_unchanged] so that passing null clears
  // them.
  WorkItem _copy({
    Object? parentId = _unchanged,
    String? statusId,
    Object? assignedToUserId = _unchanged,
    Object? startDate = _unchanged,
    Object? endDate = _unchanged,
    List<String>? tags,
  }) => WorkItem(
    id: id,
    number: number,
    spaceId: spaceId,
    parentId: identical(parentId, _unchanged)
        ? this.parentId
        : parentId as String?,
    title: title,
    description: description,
    statusId: statusId ?? this.statusId,
    typeId: typeId,
    priorityId: priorityId,
    assignedToUserId: identical(assignedToUserId, _unchanged)
        ? this.assignedToUserId
        : assignedToUserId as String?,
    startDate: identical(startDate, _unchanged)
        ? this.startDate
        : startDate as DateTime?,
    endDate: identical(endDate, _unchanged)
        ? this.endDate
        : endDate as DateTime?,
    createdAt: createdAt,
    updatedAt: updatedAt,
    version: version,
    tags: tags ?? this.tags,
    fields: fields,
    when: when,
    whenResolved: whenResolved,
    recurrenceSourceId: recurrenceSourceId,
    recurrenceDate: recurrenceDate,
  );
}
