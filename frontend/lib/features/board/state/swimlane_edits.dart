import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Pure edits over a board's lanes, used for optimistic updates and their
/// reverts. Every edit finds its card by id, so it still applies after
/// other changes have moved things around.
extension SwimlaneEdits on List<Swimlane> {
  bool hasLane(String parentId) => any((lane) => lane.parentId == parentId);

  List<Swimlane> withCard(
    String cardId,
    WorkItemCard Function(WorkItemCard card) update,
  ) => [
    for (final lane in this)
      lane.copyWithCards([
        for (final c in lane.cards)
          if (c.id == cardId) update(c) else c,
      ]),
  ];

  /// Moves the card from [fromParentId]'s lane to [toParentId]'s, appended
  /// unless [atIndex] is given. A no-op if the card isn't in the source lane
  /// (e.g. a later move already took it elsewhere).
  List<Swimlane> withCardMovedToLane(
    String cardId,
    String fromParentId,
    String toParentId, {
    int? atIndex,
  }) {
    final card = where(
      (lane) => lane.parentId == fromParentId,
    ).firstOrNull?.cards.where((c) => c.id == cardId).firstOrNull;
    if (card == null) return this;

    final moved = card.movedToParent(toParentId);
    List<WorkItemCard> insertedInto(List<WorkItemCard> cards) {
      final index = (atIndex ?? cards.length).clamp(0, cards.length);
      return [...cards]..insert(index, moved);
    }

    return [
      for (final lane in this)
        if (lane.parentId == fromParentId)
          lane.copyWithCards([
            for (final c in lane.cards)
              if (c.id != cardId) c,
          ])
        else if (lane.parentId == toParentId)
          lane.copyWithCards(insertedInto(lane.cards))
        else
          lane,
    ];
  }

  /// Sets the assignee wherever [workItemId] appears: as a lane's own item,
  /// a card, or both.
  List<Swimlane> withAssignee(String workItemId, String? userId) => [
    for (final lane in this)
      (lane.parentId == workItemId ? lane.assigned(userId) : lane)
          .copyWithCards([
            for (final card in lane.cards)
              if (card.id == workItemId) card.assigned(userId) else card,
          ]),
  ];

  String? assigneeOf(String workItemId) {
    for (final lane in this) {
      if (lane.parentId == workItemId) return lane.assignedToUserId;
      for (final card in lane.cards) {
        if (card.id == workItemId) return card.assignedToUserId;
      }
    }
    return null;
  }

  List<String>? tagsOf(String workItemId) {
    for (final lane in this) {
      for (final card in lane.cards) {
        if (card.id == workItemId) return card.tags;
      }
    }
    return null;
  }
}
