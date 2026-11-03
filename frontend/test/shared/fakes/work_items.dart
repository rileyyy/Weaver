import 'package:weaver/shared/models/work_item.dart';

/// A [WorkItem] with test-friendly defaults for every field a test doesn't
/// care about.
WorkItem workItem(
  String id, {
  int number = 1,
  String? parentId,
  String? title,
  String statusId = 'todo',
  String? description,
  String? layerId,
  String? priorityId,
  String? assignedToUserId,
  DateTime? startDate,
  DateTime? endDate,
  List<String> tags = const [],
  int version = 1,
}) => WorkItem(
  id: id,
  number: number,
  parentId: parentId,
  title: title ?? id,
  description: description,
  statusId: statusId,
  layerId: layerId,
  priorityId: priorityId,
  assignedToUserId: assignedToUserId,
  startDate: startDate,
  endDate: endDate,
  createdAt: DateTime.utc(2026, 1, 1),
  updatedAt: DateTime.utc(2026, 1, 1),
  version: version,
  tags: tags,
);
