/// One work item as shown in the Hierarchy view: enough fields to render a
/// row and to nest it under its parent. A richer, unscoped sibling of
/// [WorkItemCard] — [parentId] is nullable here because, unlike the board
/// (which only ever loads a swimlane's direct children), the Hierarchy view
/// loads every work item, including true top-level items with no parent.
class HierarchyItem {
  const HierarchyItem({
    required this.id,
    required this.parentId,
    required this.title,
    required this.statusId,
    this.description,
    this.startDate,
    this.endDate,
  });

  final String id;
  final String? parentId;
  final String title;
  final String statusId;
  final String? description;
  final DateTime? startDate;
  final DateTime? endDate;
}

/// A [HierarchyItem] together with its own children, already filtered and
/// ordered — the shape the Hierarchy view renders directly.
class HierarchyNode {
  const HierarchyNode({required this.item, required this.children});

  final HierarchyItem item;
  final List<HierarchyNode> children;
}
