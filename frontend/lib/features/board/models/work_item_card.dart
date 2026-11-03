import 'package:weaver/shared/models/work_item.dart';

/// A card on the board: the subset of a work item's fields the board needs
/// to render. [parentId] is the swimlane's work item id — never mutated by
/// [copyWith], since a status change must never become a reparent (see
/// project_design.md's Board rules).
class WorkItemCard {
  const WorkItemCard({
    required this.id,
    required this.number,
    required this.title,
    required this.parentId,
    required this.statusId,
    this.description,
    this.startDate,
    this.endDate,
    this.assignedToUserId,
    this.layerId,
    this.tags = const [],
  });

  /// [item] drawn as a card in the lane of [laneId], which owns it on the
  /// board whatever [WorkItem.parentId] says.
  factory WorkItemCard.fromWorkItem(WorkItem item, {required String laneId}) =>
      WorkItemCard(
        id: item.id,
        number: item.number,
        title: item.title,
        parentId: laneId,
        statusId: item.statusId,
        description: item.description,
        startDate: item.startDate,
        endDate: item.endDate,
        assignedToUserId: item.assignedToUserId,
        layerId: item.layerId,
        tags: item.tags,
      );

  final String id;

  /// The short, sequential, human-facing id (e.g. "#42") shown on the card
  /// — see the backend's `WorkItem.Number` for why this exists alongside
  /// [id].
  final int number;

  final String title;
  final String parentId;
  final String statusId;
  final String? description;

  /// When this item is scheduled to start/end. Either may be null — an
  /// open start or end is unbounded on that side for a time-frame filter,
  /// not "never scheduled."
  final DateTime? startDate;
  final DateTime? endDate;

  final String? assignedToUserId;

  /// Which layer (e.g. Project / Goal / Task) this item is labelled with,
  /// if any; its colour marks the item on the board.
  final String? layerId;

  /// Short free-text labels, zero or more.
  final List<String> tags;

  WorkItemCard copyWith({String? statusId}) => WorkItemCard(
    id: id,
    number: number,
    title: title,
    parentId: parentId,
    statusId: statusId ?? this.statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    layerId: layerId,
    tags: tags,
  );

  /// Moves this card to a new parent, keeping its status — the model-level
  /// mirror of the backend's `Reparent` (never touches [statusId]), the same
  /// way [copyWith] never touches [parentId].
  WorkItemCard movedToParent(String newParentId) => WorkItemCard(
    id: id,
    number: number,
    title: title,
    parentId: newParentId,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    layerId: layerId,
    tags: tags,
  );
}
