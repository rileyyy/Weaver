import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_layout.dart';
import 'package:weaver/features/board/views/swimlane/widgets/card_drop_side.dart';

const cardDropIndicatorKey = Key('card-drop-indicator');

/// Splits [child], a card in a status cell, into a leading and a trailing
/// drop zone, so a dragged card can be dropped before or after it.
///
/// Cards sit two to a row, so the halves are left and right: in reading
/// order that is the same as before and after. The zones are drag targets
/// drawn over the card that never claim a tap, so the card underneath
/// still opens and drags as usual.
class CardDropZones extends StatelessWidget {
  const CardDropZones({
    super.key,
    required this.accepts,
    required this.movesCard,
    required this.onDropped,
    required this.child,
  });

  final bool Function(WorkItemCard dragged) accepts;

  /// Whether dropping on [CardDropSide] would put the dragged card somewhere
  /// new. A drop that wouldn't is still accepted, so it can't fall through
  /// to the cell and send the card to the end, but shows no indicator.
  final bool Function(WorkItemCard dragged, CardDropSide side) movesCard;
  final void Function(WorkItemCard dragged, CardDropSide side) onDropped;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: [
        child,
        Positioned.fill(
          child: Row(
            children: [
              Expanded(child: _zone(CardDropSide.before)),
              Expanded(child: _zone(CardDropSide.after)),
            ],
          ),
        ),
      ],
    );
  }

  Widget _zone(CardDropSide side) {
    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) => accepts(details.data),
      onAcceptWithDetails: (details) {
        if (movesCard(details.data, side)) onDropped(details.data, side);
      },
      builder: (context, candidateData, rejectedData) {
        final dragged = candidateData.firstOrNull;
        if (dragged == null || !movesCard(dragged, side)) {
          return const SizedBox.expand();
        }
        return _indicator(context, side);
      },
    );
  }

  /// A bar centred in the gap beside the card, where the dropped card goes.
  Widget _indicator(BuildContext context, CardDropSide side) {
    const width = 3.0;
    const offset = (cardGridSpacing + width) / 2;
    return IgnorePointer(
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned(
            key: cardDropIndicatorKey,
            top: 0,
            bottom: 0,
            left: side == CardDropSide.before ? -offset : null,
            right: side == CardDropSide.after ? -offset : null,
            width: width,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.primary,
                borderRadius: BorderRadius.circular(width / 2),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
