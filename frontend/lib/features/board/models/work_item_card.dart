import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_fields.dart';

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
    this.tags = const [],
  });

  factory WorkItemCard.fromJson(Map<String, dynamic> json) => WorkItemCard(
    id: json['id'] as String,
    number: json['number'] as int,
    title: json['title'] as String,
    parentId: json['parentId'] as String,
    statusId: json['statusId'] as String,
    description: json['description'] as String?,
    startDate: parseCalendarDate(json['startDate']),
    endDate: parseCalendarDate(json['endDate']),
    assignedToUserId: json['assignedToUserId'] as String?,
    tags: stringList(json['tags']),
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

  /// Short free-text labels, zero or more — see [tagged].
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
    tags: tags,
  );

  /// Sets this card's schedule, keeping its status and parent — the
  /// model-level mirror of the backend's `Reschedule`.
  WorkItemCard rescheduled(DateTime? startDate, DateTime? endDate) =>
      WorkItemCard(
        id: id,
        number: number,
        title: title,
        parentId: parentId,
        statusId: statusId,
        description: description,
        startDate: startDate,
        endDate: endDate,
        assignedToUserId: assignedToUserId,
        tags: tags,
      );

  /// Sets this card's assignee, keeping everything else — the model-level
  /// mirror of the backend's `Assign`.
  WorkItemCard assigned(String? userId) => WorkItemCard(
    id: id,
    number: number,
    title: title,
    parentId: parentId,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: userId,
    tags: tags,
  );

  /// Sets this card's tags (the full replacement list), keeping everything
  /// else — the model-level mirror of the backend's `SetTags`.
  WorkItemCard tagged(List<String> tags) => WorkItemCard(
    id: id,
    number: number,
    title: title,
    parentId: parentId,
    statusId: statusId,
    description: description,
    startDate: startDate,
    endDate: endDate,
    assignedToUserId: assignedToUserId,
    tags: tags,
  );
}
