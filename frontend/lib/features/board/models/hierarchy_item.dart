import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';

/// One work item as shown in the Hierarchy view: enough fields to render a
/// row and to nest it under its parent. A richer, unscoped sibling of
/// [WorkItemCard] — [parentId] is nullable here because, unlike the board
/// (which only ever loads a swimlane's direct children), the Hierarchy view
/// loads every work item, including true top-level items with no parent.
class HierarchyItem {
  const HierarchyItem({
    required this.id,
    required this.number,
    required this.parentId,
    required this.title,
    required this.statusId,
    this.description,
    this.startDate,
    this.endDate,
    this.assignedToUserId,
    this.tags = const [],
  });

  factory HierarchyItem.fromJson(Map<String, dynamic> json) => HierarchyItem(
    id: json['id'] as String,
    number: json['number'] as int,
    parentId: json['parentId'] as String?,
    title: json['title'] as String,
    statusId: json['statusId'] as String,
    description: json['description'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    assignedToUserId: json['assignedToUserId'] as String?,
    tags: stringList(json['tags']),
  );

  final String id;

  /// The short, sequential, human-facing id (e.g. "#42") — mirrors
  /// [WorkItemCard.number].
  final int number;

  final String? parentId;
  final String title;
  final String statusId;
  final String? description;
  final DateTime? startDate;
  final DateTime? endDate;
  final String? assignedToUserId;

  /// Short free-text labels, zero or more — see [tagged].
  final List<String> tags;

  /// Sets this item's assignee, keeping everything else — the model-level
  /// mirror of the backend's `Assign`.
  HierarchyItem assigned(String? userId) => HierarchyItem(
    id: id,
    number: number,
    parentId: parentId,
    title: title,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: userId,
    tags: tags,
  );

  /// Sets this item's tags (the full replacement list), keeping everything
  /// else — the model-level mirror of the backend's `SetTags`.
  HierarchyItem tagged(List<String> tags) => HierarchyItem(
    id: id,
    number: number,
    parentId: parentId,
    title: title,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    tags: tags,
  );

  /// Moves this item to another status column — mirrors the backend's
  /// `ChangeStatus`, so the Hierarchy/Roadmap views follow board drags.
  HierarchyItem withStatus(String statusId) => HierarchyItem(
    id: id,
    number: number,
    parentId: parentId,
    title: title,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    tags: tags,
  );

  /// Mirrors the backend's `Reparent`.
  HierarchyItem movedToParent(String? parentId) => HierarchyItem(
    id: id,
    number: number,
    parentId: parentId,
    title: title,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    tags: tags,
  );

  /// Mirrors the backend's `Reschedule`.
  HierarchyItem rescheduled(DateTime? startDate, DateTime? endDate) =>
      HierarchyItem(
        id: id,
        number: number,
        parentId: parentId,
        title: title,
        statusId: statusId,
        description: description,
        startDate: startDate,
        endDate: endDate,
        assignedToUserId: assignedToUserId,
        tags: tags,
      );
}

/// A [HierarchyItem] together with its own children, already filtered and
/// ordered — the shape the Hierarchy view renders directly.
class HierarchyNode {
  const HierarchyNode({required this.item, required this.children});

  final HierarchyItem item;
  final List<HierarchyNode> children;
}
