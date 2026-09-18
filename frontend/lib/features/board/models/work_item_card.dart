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
    this.description,
    this.startDate,
    this.endDate,
  });

  final String id;
  final String title;
  final String parentId;
  final String statusId;
  final String? description;

  /// When this item is scheduled to start/end. Either may be null — an
  /// open start or end is unbounded on that side for a time-frame filter,
  /// not "never scheduled."
  final DateTime? startDate;
  final DateTime? endDate;

  WorkItemCard copyWith({String? statusId}) => WorkItemCard(
        id: id,
        title: title,
        parentId: parentId,
        statusId: statusId ?? this.statusId,
        description: description,
        startDate: startDate,
        endDate: endDate,
      );

  /// Moves this card to a new parent, keeping its status — the model-level
  /// mirror of the backend's `Reparent` (never touches [statusId]), the same
  /// way [copyWith] never touches [parentId].
  WorkItemCard movedToParent(String newParentId) => WorkItemCard(
        id: id,
        title: title,
        parentId: newParentId,
        statusId: statusId,
        description: description,
        startDate: startDate,
        endDate: endDate,
      );

  /// Sets this card's schedule, keeping its status and parent — the
  /// model-level mirror of the backend's `Reschedule`.
  WorkItemCard rescheduled(DateTime? startDate, DateTime? endDate) =>
      WorkItemCard(
        id: id,
        title: title,
        parentId: parentId,
        statusId: statusId,
        description: description,
        startDate: startDate,
        endDate: endDate,
      );
}
