/// A card on the board: the subset of a work item's fields the board needs
/// to render. [parentId] is the swimlane's work item id — never mutated by
/// [copyWith], since a status change must never become a reparent (see
/// project_design.md's Board rules).
class WorkItemCard {
  const WorkItemCard({
    required this.id,
    required this.title,
    required this.parentId,
    required this.statusId,
  });

  final String id;
  final String title;
  final String parentId;
  final String statusId;

  WorkItemCard copyWith({String? statusId}) => WorkItemCard(
        id: id,
        title: title,
        parentId: parentId,
        statusId: statusId ?? this.statusId,
      );
}
