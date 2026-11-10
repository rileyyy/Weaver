import 'package:weaver/shared/models/work_item.dart';

/// A [WorkItem] with test-friendly defaults for every field a test doesn't
/// care about.
WorkItem workItem(
  String id, {
  int number = 1,
  String spaceId = 'home',
  String? parentId,
  String? title,
  String statusId = 'todo',
  String? description,
  String? typeId,
  String? priorityId,
  String? assignedToUserId,
  DateTime? startDate,
  DateTime? endDate,
  List<String> tags = const [],
  Map<String, Object?> fields = const {},
  int version = 1,
}) => WorkItem(
  id: id,
  number: number,
  spaceId: spaceId,
  parentId: parentId,
  title: title ?? id,
  description: description,
  statusId: statusId,
  typeId: typeId,
  priorityId: priorityId,
  assignedToUserId: assignedToUserId,
  startDate: startDate,
  endDate: endDate,
  createdAt: DateTime.utc(2026, 1, 1),
  updatedAt: DateTime.utc(2026, 1, 1),
  version: version,
  tags: tags,
  fields: fields,
);

/// [item] as the server would return it after a write: the same fields with
/// a new [version].
WorkItem withVersion(WorkItem item, int version) => WorkItem(
  id: item.id,
  number: item.number,
  spaceId: item.spaceId,
  parentId: item.parentId,
  title: item.title,
  description: item.description,
  statusId: item.statusId,
  typeId: item.typeId,
  priorityId: item.priorityId,
  assignedToUserId: item.assignedToUserId,
  startDate: item.startDate,
  endDate: item.endDate,
  createdAt: item.createdAt,
  updatedAt: item.updatedAt,
  version: version,
  tags: item.tags,
  recurrenceSourceId: item.recurrenceSourceId,
  recurrenceDate: item.recurrenceDate,
);
