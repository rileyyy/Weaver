import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_layout.dart';
import 'package:weaver/features/board/views/swimlane/widgets/card_drop_side.dart';

const cardDropIndicatorKey = Key('card-drop-indicator');

/// Splits [child], a card's slot in a status cell, into a leading and a
/// trailing drop zone, so a dragged card can be dropped before or after it.
///
/// [child] is expected to include the card's half of the gaps around it
/// ([cardGridSpacing] / 2 on every side): the zones cover the whole slot,
/// so a drop in a gap lands where the indicator in that gap shows.
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
    this.showsIndicatorAfter = false,
    required this.child,
  });

  final bool Function(WorkItemCard dragged) accepts;

  /// Whether a drop on that side would put the dragged card somewhere new.
  /// A drop that wouldn't is still accepted, so it can't fall through to
  /// the cell and send the card to the end, but shows no indicator.
  final bool Function(WorkItemCard dragged, CardDropSide side) movesCard;
  final void Function(WorkItemCard dragged, CardDropSide side) onDropped;

  /// Shows the trailing indicator without a drag over this card: the cell
  /// sets it on its last card while a card hovers the cell's free space,
  /// since a drop there lands at the end.
  final bool showsIndicatorAfter;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Stack(
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
        final hoveredWithAMove = dragged != null && movesCard(dragged, side);
        final forcedAfter = side == CardDropSide.after && showsIndicatorAfter;
        if (!hoveredWithAMove && !forcedAfter) {
          return const SizedBox.expand();
        }
        return _indicator(context, side);
      },
    );
  }

  Widget _indicator(BuildContext context, CardDropSide side) {
    const width = 3.0;
    // The slot's outer edge is the middle of the gap beside the card.
    const edge = -width / 2;
    return IgnorePointer(
      child: Stack(
        clipBehavior: Clip.none,
        children: [
          Positioned(
            key: cardDropIndicatorKey,
            top: cardGridSpacing / 2,
            bottom: cardGridSpacing / 2,
            left: side == CardDropSide.before ? edge : null,
            right: side == CardDropSide.after ? edge : null,
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
