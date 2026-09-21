import 'package:weaver/features/board/models/work_item_card.dart';

/// One swimlane: a parent work item and its direct children as cards.
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

  /// The swimlane's own work item (e.g. a Project-layer item) may itself
  /// have an assignee, shown on its lane label the same way a card shows
  /// its own.
  final String? assignedToUserId;

  Swimlane copyWithCards(List<WorkItemCard> cards) => Swimlane(
        parentId: parentId,
        title: title,
        cards: cards,
        assignedToUserId: assignedToUserId,
      );

  /// Sets this lane's own work item's assignee, keeping its cards.
  Swimlane assigned(String? userId) => Swimlane(
        parentId: parentId,
        title: title,
        cards: cards,
        assignedToUserId: userId,
      );
}
