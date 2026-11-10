import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

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
    this.typeId,
    this.tags = const [],
    this.fields = const {},
    this.blockedBy = const [],
  });

  factory HierarchyItem.fromWorkItem(WorkItem item) => HierarchyItem(
    id: item.id,
    number: item.number,
    parentId: item.parentId,
    title: item.title,
    statusId: item.statusId,
    description: item.description,
    startDate: item.startDate,
    endDate: item.endDate,
    assignedToUserId: item.assignedToUserId,
    typeId: item.typeId,
    tags: item.tags,
    fields: item.fields,
    blockedBy: item.blockedBy,
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

  /// Which type (e.g. Project / Goal / Task) this item is labelled with,
  /// if any; its colour marks the item on the board.
  final String? typeId;

  /// Short free-text labels, zero or more.
  final List<String> tags;

  /// The item's custom field values by field id, for its roll-ups.
  final Map<String, Object?> fields;

  /// The item's live blockers; see [WorkItem.blockedBy].
  final List<WorkItemBlocker> blockedBy;
}
