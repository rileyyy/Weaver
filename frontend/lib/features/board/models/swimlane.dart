import 'package:weaver/features/board/models/work_item_card.dart';

/// One swimlane: a parent work item and its direct children as cards.
class Swimlane {
  const Swimlane({
    required this.parentId,
    required this.title,
    required this.cards,
  });

  final String parentId;
  final String title;
  final List<WorkItemCard> cards;

  Swimlane copyWithCards(List<WorkItemCard> cards) => Swimlane(
        parentId: parentId,
        title: title,
        cards: cards,
      );
}
