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
    final cellIds = _cellIds(cardId, statusId);
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
    final cards = rest
      ..insert(_insertionIndex(rest, statusId, afterId), placed);

    return [
      for (final lane in this)
        if (lane.parentId == owner.parentId)
          lane.copyWithCards(cards)
        else
          lane,
    ];
  }

  List<String> cellIdsOf(String cardId) {
    final card = _laneOf(cardId)?.cards.firstWhere((c) => c.id == cardId);
    if (card == null) return const [];
    return _cellIds(cardId, card.statusId);
  }

  /// Puts the card back in [statusId] as close to where it was in
  /// [originalCellIds] as the cell allows now: after whichever of its old
  /// predecessors is now latest, else before whichever of its old
  /// successors is now earliest, else at the end. Its old neighbours may
  /// have moved since, and those moves must survive the undo.
  List<Swimlane> withCardRestored(
    String cardId,
    String statusId, {
    required List<String> originalCellIds,
  }) {
    final index = originalCellIds.indexOf(cardId);
    if (index == -1) return this;

    final currentIds = [
      for (final id in _cellIds(cardId, statusId))
        if (id != cardId) id,
    ];
    final predecessors = originalCellIds.take(index).toSet();
    final successors = originalCellIds.skip(index + 1).toSet();
    return withCardPlaced(
      cardId,
      statusId,
      afterId: _restoreAnchor(currentIds, predecessors, successors),
    );
  }

  static String? _restoreAnchor(
    List<String> currentIds,
    Set<String> predecessors,
    Set<String> successors,
  ) {
    final latestPredecessor = currentIds
        .where(predecessors.contains)
        .lastOrNull;
    if (latestPredecessor != null) return latestPredecessor;

    final earliestSuccessor = currentIds.indexWhere(successors.contains);
    if (earliestSuccessor == -1) return currentIds.lastOrNull;
    return earliestSuccessor == 0 ? null : currentIds[earliestSuccessor - 1];
  }

  /// Where a card goes in [rest], its lane without it: right after [afterId]
  /// if that is in the [statusId] cell, else at the top of that cell. A
  /// lane with no cards in that status can take it anywhere, so the end.
  static int _insertionIndex(
    List<WorkItemCard> rest,
    String statusId,
    String? afterId,
  ) {
    final anchorIndex = rest.indexWhere(
      (c) => c.id == afterId && c.statusId == statusId,
    );
    if (anchorIndex != -1) return anchorIndex + 1;

    final topIndex = rest.indexWhere((c) => c.statusId == statusId);
    return topIndex == -1 ? rest.length : topIndex;
  }

  /// The ids in [statusId]'s cell of the lane holding [cardId], in rank
  /// order.
  List<String> _cellIds(String cardId, String statusId) => [
    for (final card in _laneOf(cardId)?.cards ?? const <WorkItemCard>[])
      if (card.statusId == statusId) card.id,
  ];

  Swimlane? _laneOf(String cardId) =>
      where((lane) => lane.cards.any((c) => c.id == cardId)).firstOrNull;

  /// Takes the card out of whichever lane holds it, e.g. once it turns out
  /// to belong to a lane this board doesn't show.
  List<Swimlane> withoutCard(String cardId) => [
    for (final lane in this)
      lane.copyWithCards([
        for (final c in lane.cards)
          if (c.id != cardId) c,
      ]),
  ];

  /// The id of the lane holding [cardId], or null if no lane does.
  String? laneIdOf(String cardId) => _laneOf(cardId)?.parentId;
}
