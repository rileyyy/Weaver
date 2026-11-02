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

  /// The card that [cardId], dropped in its lane's [statusId] cell, should
  /// be ranked directly after: the one before [beforeCardId], or the cell's
  /// last card when [beforeCardId] is null or no longer in the cell. Null
  /// means the top of the cell, which is also what the backend reads a
  /// missing `afterId` as.
  String? rankAnchorForDrop(
    String cardId,
    String statusId, {
    String? beforeCardId,
  }) {
    final cellIds = [
      for (final card in _laneOf(cardId)?.cards ?? const <WorkItemCard>[])
        if (card.statusId == statusId) card.id,
    ];
    final beforeIndex = beforeCardId == null
        ? -1
        : cellIds.indexOf(beforeCardId);
    final idsAhead = beforeIndex == -1
        ? cellIds
        : cellIds.sublist(0, beforeIndex);
    return idsAhead.where((id) => id != cardId).lastOrNull;
  }

  /// Moves the card to [statusId] within its lane, directly after [afterId]
  /// in that cell, or at the top of the cell when [afterId] is null or
  /// isn't in the cell. No other card moves.
  List<Swimlane> withCardPlaced(
    String cardId,
    String statusId, {
    required String? afterId,
  }) {
    final owner = _laneOf(cardId);
    if (owner == null) return this;

    final placed = owner.cards
        .firstWhere((c) => c.id == cardId)
        .copyWith(statusId: statusId);
    final rest = [
      for (final c in owner.cards)
        if (c.id != cardId) c,
    ];
    final anchorIndex = rest.indexWhere(
      (c) => c.id == afterId && c.statusId == statusId,
    );
    final topIndex = rest.indexWhere((c) => c.statusId == statusId);
    final insertAt = anchorIndex != -1
        ? anchorIndex + 1
        : (topIndex == -1 ? rest.length : topIndex);
    final cards = rest..insert(insertAt, placed);

    return [
      for (final lane in this)
        if (lane.parentId == owner.parentId)
          lane.copyWithCards(cards)
        else
          lane,
    ];
  }

  /// The ids of the cards before [cardId] in its cell, in rank order.
  List<String> cellIdsBefore(String cardId) {
    final cards = _laneOf(cardId)?.cards ?? const <WorkItemCard>[];
    final card = cards.where((c) => c.id == cardId).firstOrNull;
    if (card == null) return const [];
    return [
      for (final c in cards.takeWhile((c) => c.id != cardId))
        if (c.statusId == card.statusId) c.id,
    ];
  }

  /// Puts the card back in [statusId] after the last of [predecessorIds]
  /// still in that cell, so undoing a move doesn't depend on its old
  /// neighbour not having moved since.
  List<Swimlane> withCardRestored(
    String cardId,
    String statusId, {
    required List<String> predecessorIds,
  }) {
    final cellIds = {
      for (final card in _laneOf(cardId)?.cards ?? const <WorkItemCard>[])
        if (card.statusId == statusId && card.id != cardId) card.id,
    };
    return withCardPlaced(
      cardId,
      statusId,
      afterId: predecessorIds.where(cellIds.contains).lastOrNull,
    );
  }

  Swimlane? _laneOf(String cardId) =>
      where((lane) => lane.cards.any((c) => c.id == cardId)).firstOrNull;

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
