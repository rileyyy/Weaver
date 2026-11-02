import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_layout.dart';
import 'package:weaver/features/board/views/swimlane/widgets/card_drop_side.dart';
import 'package:weaver/features/board/views/swimlane/widgets/card_drop_zones.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// A drop on a card passes the card it landed before ([beforeCardId] null
/// for the end of the cell); a drop on the cell's free space passes none,
/// which also means the end.
typedef CardDropCallback =
    Future<void> Function(
      WorkItemCard card,
      String newStatusId, {
      String? beforeCardId,
    });

class StatusColumn extends StatelessWidget {
  const StatusColumn({
    super.key,
    required this.swimlane,
    required this.status,
    required this.cardWidth,
    required this.cardHeight,
    required this.isCollapsed,
    required this.onCardDropped,
    required this.onCardDetailsOpened,
    required this.onAssignRequested,
    required this.onAddTagsRequested,
    required this.cardVisible,
    required this.cardComparator,
    required this.assigneeInitialFor,
    required this.layerColorFor,
  });

  final Swimlane swimlane;
  final WorkItemStatus status;

  /// Precomputed by [SwimlaneView] (shared across every column so a
  /// swimlane's row height, also computed there, matches what actually
  /// renders here).
  final double cardWidth;
  final double cardHeight;

  final bool isCollapsed;

  final CardDropCallback onCardDropped;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final void Function(String workItemId, String? currentAssigneeId)
  onAssignRequested;
  final void Function(String workItemId, List<String> currentTags)
  onAddTagsRequested;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;
  final String? Function(String? userId) assigneeInitialFor;
  final Color? Function(String? layerId) layerColorFor;

  @override
  Widget build(BuildContext context) {
    if (isCollapsed) {
      final count = swimlane.cards
          .where((c) => c.statusId == status.id && cardVisible(c))
          .length;
      return Container(
        margin: const EdgeInsets.all(statusColumnMargin),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerLow,
          borderRadius: BorderRadius.circular(8),
        ),
        child: count == 0
            ? null
            : Text(
                count == 1 ? '1 card' : '$count cards',
                style: Theme.of(context).textTheme.bodySmall,
              ),
      );
    }

    final cards = swimlane.cards
        .where((c) => c.statusId == status.id && cardVisible(c))
        .toList();
    final comparator = cardComparator;
    if (comparator != null) cards.sort(comparator);
    // A sorted cell isn't shown in rank order, so a position in it means
    // nothing to the backend: cards can only be moved in from elsewhere.
    final isManualOrder = comparator == null;

    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          _isInThisLane(details.data) &&
          (isManualOrder || details.data.statusId != status.id),
      onAcceptWithDetails: (details) =>
          unawaited(onCardDropped(details.data, status.id)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        final draggedOverFreeSpace = candidateData.firstOrNull;
        final endIndicatorShown =
            isManualOrder &&
            draggedOverFreeSpace != null &&
            cards.isNotEmpty &&
            cards.last.id != draggedOverFreeSpace.id;
        return Container(
          margin: const EdgeInsets.all(statusColumnMargin),
          // Each card slot pads itself by the other half of the spacing, so
          // the gaps between cards belong to the cards' drop zones and a
          // drop in a gap lands where its indicator shows.
          padding: const EdgeInsets.all(cardGridSpacing / 2),
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          // The row's height (computed by SwimlaneView) already fits the
          // busiest column's cards plus a free slot, so this Wrap never
          // needs to scroll internally; it just fills the space given.
          child: Wrap(
            children: [
              for (final card in cards)
                isManualOrder
                    ? _withDropZones(
                        cards,
                        card,
                        showsIndicatorAfter:
                            endIndicatorShown && card == cards.last,
                      )
                    : _cardSlot(card),
            ],
          ),
        );
      },
    );
  }

  bool _isInThisLane(WorkItemCard card) => card.parentId == swimlane.parentId;

  Widget _cardSlot(WorkItemCard card) => Padding(
    padding: const EdgeInsets.all(cardGridSpacing / 2),
    child: SizedBox(
      width: cardWidth,
      child: BoardCard(
        card: card,
        height: cardHeight,
        assigneeInitial: assigneeInitialFor(card.assignedToUserId),
        layerColor: layerColorFor(card.layerId),
        onOpenDetails: () => onCardDetailsOpened(card),
        onAssignTapped: () => onAssignRequested(card.id, card.assignedToUserId),
        onAddTagTapped: () => onAddTagsRequested(card.id, card.tags),
      ),
    ),
  );

  Widget _withDropZones(
    List<WorkItemCard> shownCards,
    WorkItemCard card, {
    required bool showsIndicatorAfter,
  }) {
    final index = shownCards.indexOf(card);
    // The shown cards either side of the gap a drop on [side] fills; null
    // past either end of the cell.
    WorkItemCard? followingDrop(CardDropSide side) {
      final position = side == CardDropSide.before ? index : index + 1;
      return position < shownCards.length ? shownCards[position] : null;
    }

    WorkItemCard? precedingDrop(CardDropSide side) {
      final position = side == CardDropSide.before ? index - 1 : index;
      return position >= 0 ? shownCards[position] : null;
    }

    return CardDropZones(
      accepts: _isInThisLane,
      showsIndicatorAfter: showsIndicatorAfter,
      movesCard: (dragged, side) =>
          dragged.statusId != status.id ||
          (precedingDrop(side)?.id != dragged.id &&
              followingDrop(side)?.id != dragged.id),
      onDropped: (dragged, side) => unawaited(
        onCardDropped(
          dragged,
          status.id,
          beforeCardId: followingDrop(side)?.id,
        ),
      ),
      child: _cardSlot(card),
    );
  }
}
