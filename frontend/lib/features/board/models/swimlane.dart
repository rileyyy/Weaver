import 'package:flutter/foundation.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// One swimlane: a parent work item and its direct children as cards.
///
/// Compared by value so a lane redrawn from the store only notifies its
/// listeners when something it shows actually changed.
@immutable
class Swimlane {
  const Swimlane({
    required this.parentId,
    required this.title,
    required this.cards,
    this.assignedToUserId,
  });

  final String parentId;
  final String title;
  final List<WorkItemCard> cards;

  /// The swimlane's own work item (e.g. a Project-type item) may itself
  /// have an assignee, shown on its lane label the same way a card shows
  /// its own.
  final String? assignedToUserId;

  Swimlane copyWithCards(List<WorkItemCard> cards) => Swimlane(
    parentId: parentId,
    title: title,
    cards: cards,
    assignedToUserId: assignedToUserId,
  );

  @override
  bool operator ==(Object other) =>
      other is Swimlane &&
      other.parentId == parentId &&
      other.title == title &&
      other.assignedToUserId == assignedToUserId &&
      listEquals(other.cards, cards);

  @override
  int get hashCode =>
      Object.hash(parentId, title, assignedToUserId, Object.hashAll(cards));
}
