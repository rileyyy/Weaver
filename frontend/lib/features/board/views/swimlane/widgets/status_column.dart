import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/shared/models/work_item_status.dart';

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
    required this.cardVisible,
    required this.cardComparator,
    required this.assigneeInitialFor,
  });

  final Swimlane swimlane;
  final WorkItemStatus status;

  /// Precomputed by [SwimlaneBoard] (shared across every column so a
  /// swimlane's row height, also computed there, matches what actually
  /// renders here).
  final double cardWidth;
  final double cardHeight;

  final bool isCollapsed;

  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final void Function(String workItemId, String? currentAssigneeId)
  onAssignRequested;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;
  final String? Function(String? userId) assigneeInitialFor;

  @override
  Widget build(BuildContext context) {
    if (isCollapsed) {
      final count = swimlane.cards
          .where((c) => c.statusId == status.id && cardVisible(c))
          .length;
      return Container(
        margin: const EdgeInsets.all(4),
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

    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId == swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardDropped(details.data, status.id)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        return Container(
          margin: const EdgeInsets.all(4),
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          // The row's own height (computed by SwimlaneBoard) already
          // reserves at least a 2x2 grid, growing for however many rows
          // this cell's card count actually needs — so this Wrap never
          // needs to scroll internally, it just fills the space given.
          child: Wrap(
            spacing: SwimlaneView.cardGridSpacing,
            runSpacing: SwimlaneView.cardGridSpacing,
            children: [
              for (final card in cards)
                SizedBox(
                  width: cardWidth,
                  child: BoardCard(
                    card: card,
                    height: cardHeight,
                    assigneeInitial: assigneeInitialFor(card.assignedToUserId),
                    onOpenDetails: () => onCardDetailsOpened(card),
                    onAssignTapped: () =>
                        onAssignRequested(card.id, card.assignedToUserId),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}
